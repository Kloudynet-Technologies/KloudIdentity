using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using KN.KloudIdentity.Mapper.Domain.SQL.Constants;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Http;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class SQLIntegration : IIntegrationBaseV2
{
    /// <summary>
    /// SourceValue of an attribute row that declares a stored procedure OUTPUT parameter.
    /// Accepted forms: "Output" or "Output:&lt;Role&gt;" (case-insensitive).
    /// </summary>
    public const string OutputMarker = "Output";

    /// <summary>
    /// Output role carrying the stored procedure status code.
    /// </summary>
    public const string ResponseCodeRole = "ResponseCode";

    /// <summary>
    /// Output role carrying the stored procedure status message.
    /// </summary>
    public const string ResponseMessageRole = "ResponseMessage";

    protected const string UrnPrefix = "urn:kn:ki:schema:";

    public IntegrationMethods IntegrationMethod { get; init; }
    private readonly IOptions<AppSettings> _appSettings;

    public SQLIntegration(IOptions<AppSettings> appSettings)
    {
        IntegrationMethod = IntegrationMethods.SQL;
        _appSettings = appSettings;
    }

    public async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default, params dynamic[] args)
    {
        // AuthenticationDetails is JObject (Newtonsoft), JsonElement (System.Text.Json, e.g. when loaded
        // from AppConfigSnapshots) or a JSON string. Work with it as object and deserialize into the typed
        // model before validating, since dynamic operators/members (== null, .Driver) fail on JsonElement.
        object? authenticationDetailsJson = config.AuthenticationDetails;
        if (IsMissing(authenticationDetailsJson))
        {
            Log.Error("Authentication details are missing. AppId: {AppId}", config.AppId);
            throw new ArgumentNullException("Authentication details are missing.");
        }

        SQLAuthentication? authenticationDetails;
        try
        {
            authenticationDetails =
                JsonConvert.DeserializeObject<SQLAuthentication>(authenticationDetailsJson!.ToString()!);
        }
        catch (JsonException ex)
        {
            Log.Error("Invalid authentication details. AppId: {AppId}", config.AppId);
            throw new ArgumentException("Invalid authentication details.", ex);
        }

        if (authenticationDetails?.Driver == null || authenticationDetails.Database == null ||
            authenticationDetails.Server == null || authenticationDetails.UID == null ||
            authenticationDetails.PWD == null)
        {
            Log.Error("Invalid authentication details. AppId: {AppId}", config.AppId);
            throw new ArgumentNullException("Invalid authentication details.");
        }

        string connectionString = DatabaseConnectionUtil.GetConnectionString(authenticationDetails);

        var connection = new OdbcConnection(connectionString);
        return await Task.FromResult(connection);
    }

    public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
    {
        ValidateAttributeSchema(schema);

        if (resource == null)
            throw new ArgumentNullException("Invalid resource");

        var parameters = new List<OdbcParameter>();

        foreach (var attribute in schema)
        {
            // Output-marker rows declare SP OUTPUT parameters; the marker literal is never bound as an input.
            if (IsOutputMapping(attribute))
                continue;

            dynamic? value = null;

            if (attribute.MappingType == MappingTypes.Direct)
            {
                value = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, attribute) ?? DBNull.Value;
            }
            else if (attribute.MappingType == MappingTypes.Constant)
            {
                value = attribute.SourceValue != null ? attribute.SourceValue : DBNull.Value;
            }

            var parameter = CreateOdbcParameter(GetParameterName(attribute),
                attribute.DestinationType.ToOdbcType(), attribute.DestinationTypeLength, value);
            parameters.Add(parameter);
        }

        // Return the prepared SQL Parameter
        return await Task.FromResult(parameters);
    }

    public virtual Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource, AppConfig appConfig, CancellationToken cancellationToken = default)
    {
        return MapAndPreparePayloadAsync(schema, resource, cancellationToken);
    }

    /// <summary>
    /// Maps attribute rows to named input parameters for <see cref="ExecuteProcedureAsync"/>. Tolerates rows
    /// created by MgtPortal schema detection (one row per SP parameter):
    /// <list type="bullet">
    /// <item>Rows with an empty SourceValue are skipped, so the SP default applies.</item>
    /// <item>Output-marker rows are skipped (they are outputs, never inputs).</item>
    /// <item>A mapped row whose value is missing is still emitted, as DBNull (or its DefaultValue, except "N/A").</item>
    /// </list>
    /// Values are read with <see cref="JSONParserUtilV2{T}.ReadProperty"/> and converted here, so the
    /// detection defaults (IsRequired = true, DefaultValue = "N/A") never reach the procedure.
    /// </summary>
    protected List<OdbcParameter> MapProcedureInputs(IEnumerable<AttributeSchema> schema,
        Core2EnterpriseUser resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var parameters = new List<OdbcParameter>();

        foreach (var attribute in schema.Where(IsProcedureInputRow))
        {
            parameters.Add(CreateInputParameter(attribute, MapInputValue(attribute, resource, applyDefault: true)));
        }

        return parameters;
    }

    /// <summary>
    /// True for rows that map an input parameter: a SourceValue and DestinationField are set and the row is not
    /// an output marker.
    /// </summary>
    protected static bool IsProcedureInputRow(AttributeSchema attribute)
    {
        return !string.IsNullOrWhiteSpace(attribute.SourceValue) && !string.IsNullOrWhiteSpace(attribute.DestinationField)
               && !IsOutputMapping(attribute);
    }

    /// <summary>
    /// Value of one input row: the Constant, or the Direct value read from the resource, converted to the
    /// DestinationType (missing → DBNull). With <paramref name="applyDefault"/>, a missing value falls back to
    /// the row's DefaultValue (except "N/A").
    /// </summary>
    protected object MapInputValue(AttributeSchema attribute, Core2EnterpriseUser resource, bool applyDefault)
    {
        object? rawValue = attribute.MappingType == MappingTypes.Constant
            ? attribute.SourceValue
            : JSONParserUtilV2<Core2EnterpriseUser>.ReadProperty(resource, attribute.SourceValue.Trim());

        var value = ConvertInputValue(attribute, rawValue);

        if (applyDefault && value is DBNull && HasDefaultValue(attribute))
            value = ConvertInputValue(attribute with { MappingType = MappingTypes.Constant }, attribute.DefaultValue);

        return value;
    }

    protected static OdbcParameter CreateInputParameter(AttributeSchema attribute, object value)
    {
        return CreateOdbcParameter(GetParameterName(attribute), attribute.DestinationType.ToOdbcType(),
            attribute.DestinationTypeLength, value);
    }

    /// <summary>
    /// True for string destination types (a value that can be cleared by sending an empty string).
    /// </summary>
    protected static bool IsStringType(AttributeDataTypes dataType)
    {
        return dataType is AttributeDataTypes.String or AttributeDataTypes.NVarChar or AttributeDataTypes.VarChar
            or AttributeDataTypes.Char or AttributeDataTypes.NChar or AttributeDataTypes.Text or AttributeDataTypes.NText;
    }

    /// <summary>
    /// Converts a mapped value to the CLR type of the row's DestinationType; missing values become DBNull.
    /// A Direct value that cannot be converted becomes DBNull (logged); an invalid Constant is a config error.
    /// </summary>
    /// <exception cref="ArgumentException">A Constant value cannot be converted to the DestinationType.</exception>
    protected virtual object ConvertInputValue(AttributeSchema attribute, object? rawValue)
    {
        if (rawValue is null or DBNull)
            return DBNull.Value;

        if (rawValue is string text)
        {
            text = text.Trim();
            if (text.Length == 0)
                return DBNull.Value;
            rawValue = text;
        }

        try
        {
            return attribute.DestinationType switch
            {
                AttributeDataTypes.Int => Convert.ToInt32(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.Number or AttributeDataTypes.BigInt => Convert.ToInt64(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.SmallInt => Convert.ToInt16(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.TinyInt => Convert.ToByte(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.Boolean or AttributeDataTypes.Bit => Convert.ToBoolean(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.Decimal or AttributeDataTypes.Numeric => Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.Double => Convert.ToDouble(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.Real => Convert.ToSingle(rawValue, CultureInfo.InvariantCulture),
                AttributeDataTypes.UniqueIdentifier => rawValue as Guid? ?? Guid.Parse(rawValue.ToString()!),
                AttributeDataTypes.DateTime or AttributeDataTypes.SmallDateTime or AttributeDataTypes.Date
                    or AttributeDataTypes.Time => ToDateTimeOrDbNull(rawValue),
                _ => rawValue.ToString() is { Length: > 0 } value ? value.Trim() : DBNull.Value
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            if (attribute.MappingType == MappingTypes.Constant)
            {
                throw new ArgumentException(
                    $"Value of '{GetParameterName(attribute)}' cannot be converted to {attribute.DestinationType}.", ex);
            }

            Log.Warning("Mapped value for {Parameter} cannot be converted to {DestinationType}; sending NULL.",
                GetParameterName(attribute), attribute.DestinationType);
            return DBNull.Value;
        }
    }

    private static object ToDateTimeOrDbNull(object rawValue)
    {
        var dateTime = rawValue switch
        {
            DateTime value => value,
            DateTimeOffset value => value.UtcDateTime,
            _ => DateTime.Parse(rawValue.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        };

        // SQL DATETIME cannot hold DateTime.MinValue (the default of an unset value)
        return dateTime == DateTime.MinValue ? DBNull.Value : dateTime;
    }

    /// <summary>
    /// True when the row has a usable DefaultValue. "N/A" is the placeholder written by MgtPortal schema detection.
    /// </summary>
    private static bool HasDefaultValue(AttributeSchema attribute)
    {
        return !string.IsNullOrWhiteSpace(attribute.DefaultValue)
               && !attribute.DefaultValue.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase);
    }

    protected static OdbcParameter CreateOdbcParameter(string destinationField, OdbcType destinationType,
        int? destinationTypeLength, dynamic? value)
    {
        var parameter = new OdbcParameter(destinationField, destinationType)
        {
            Value = value ?? DBNull.Value // Handle null values
        };

        // Customize parameter properties based on type
        switch (destinationType)
        {
            case OdbcType.VarChar:
            case OdbcType.NVarChar:
            case OdbcType.Char:
            case OdbcType.NChar:
                // Default value 100 is set as an assumption if destinationTypeLength is not available
                // This won't gurantee the truncation of the value if it exceeds the length
                if (!destinationTypeLength.HasValue)
                {
                    Log.Warning("Destination type length is not provided. Defaulting to 100.");
                }

                parameter.Size = destinationTypeLength.HasValue ? destinationTypeLength.Value : 100;
                break;
            case OdbcType.Decimal:
                parameter.Precision = 18; // Default precision
                parameter.Scale = 2; // Default scale
                Log.Warning("Decimal type detected. Default precision: {Precision}, Default scale: {Scale}.",
                    parameter.Precision, parameter.Scale);
                break;
        }

        return parameter;
    }

    public virtual async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig,
        string correlationId, CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);
        // Ensure parameters are extracted from payload
        var parameters = payload as List<OdbcParameter> ??
                         throw new ArgumentNullException("No valid SqlParameter found in the provided payload.");

        if (!parameters.Any())
        {
            Log.Error(
                "No valid SqlParameter found in the provided parameters. AppId: {AppId}, CorrelationId: {CorrelationId}",
                appConfig.AppId, correlationId);
            throw new ArgumentNullException("No valid SqlParameter found in the provided parameters.");
        }

        var integrationDetails = GetIntegrationDetails(appConfig);

        var storedProcedureName = integrationDetails.PostSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        await using var connection = await OpenConnectionAsync(appConfig, correlationId, cancellationToken);

        var dbConn = DbConnectionFactory.Create(connection);
        await using var command = dbConn.CreateCommand(storedProcedureName, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);

        Log.Information("Provisioning completed for user creation. AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId, correlationId);

        return null;
    }

    public virtual Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, string appId, AppConfig appConfig,
        ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        // Action steps are not used by SQL integrations
        return ProvisionAsync(payload, appConfig, correlationId, cancellationToken);
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, Array.Empty<string>()));
    }

    public virtual Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        throw new NotSupportedException("Replace operation not supported for SQL Integration");
    }

    public virtual Task<Core2EnterpriseUser> ReplaceAsync(dynamic payload, Core2EnterpriseUser resource,
        string appId, AppConfig appConfig, ActionStep actionStep, string correlationId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Replace operation not supported for SQL Integration");
    }

    public virtual async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        // Ensure parameters are extracted from payload
        var parameters = payload as List<OdbcParameter> ??
                         throw new ArgumentNullException("No valid SqlParameter found in the provided payload.");

        if (!parameters.Any())
        {
            Log.Error(
                "No valid SqlParameter found in the provided parameters. AppId: {AppId}, CorrelationId: {CorrelationId}",
                appConfig.AppId, correlationId);
            throw new ArgumentNullException(
                $"No valid SqlParameter found in the provided parameters. AppId: {appConfig.AppId}, CorrelationId: {correlationId}");
        }

        var integrationDetails = GetIntegrationDetails(appConfig);

        var storedProcedureName = integrationDetails.PatchSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        await using var connection = await OpenConnectionAsync(appConfig, correlationId, CancellationToken.None);

        var dbConn = DbConnectionFactory.Create(connection);
        await using var command = dbConn.CreateCommand(storedProcedureName, parameters);
        await command.ExecuteNonQueryAsync();
    }

    public virtual Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, string appId,
        AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        // Action steps are not used by SQL integrations
        return UpdateAsync(payload, resource, appConfig, correlationId);
    }

    public virtual async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Log.Error("Identifier is null or invalid. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
                correlationId);
            throw new ArgumentNullException(
                $"Identifier is null or invalid. AppId: {appConfig.AppId}, CorrelationId: {correlationId}");
        }

        var integrationDetails = GetIntegrationDetails(appConfig);

        var storedProcedureName = integrationDetails.DeleteSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        var attribute = appConfig.UserAttributeSchemas.FirstOrDefault(a => a.SourceValue == "Identifier")
                        ?? throw new ArgumentException("Matching attribute not found for 'identifier'.");

        var parameters = new List<OdbcParameter>();
        var parameter = CreateOdbcParameter(GetParameterName(attribute),
            attribute.DestinationType.ToOdbcType(), attribute.DestinationTypeLength, identifier);
        parameters.Add(parameter);

        await using var connection = await OpenConnectionAsync(appConfig, correlationId, CancellationToken.None);

        var dbConn = DbConnectionFactory.Create(connection);
        await using var command = dbConn.CreateCommand(storedProcedureName, parameters);
        await command.ExecuteNonQueryAsync();
    }

    public virtual Task DeleteAsync(string identifier, string appId, AppConfig appConfig, ActionStep actionStep,
        string correlationId, CancellationToken cancellationToken = default)
    {
        // Action steps are not used by SQL integrations
        return DeleteAsync(identifier, appConfig, correlationId);
    }

    public virtual async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig,
        string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Log.Error("Identifier is null or invalid. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
                correlationId);
            throw new ArgumentNullException(
                $"Identifier is null or Invalid. AppId: {appConfig.AppId}, Identifier: {identifier}, CorrelationId: {correlationId}");
        }

        if (IsMissing(appConfig.IntegrationDetails))
        {
            Log.Error(
                "Integration details are missing. AppId: {AppId}, Identifier: {Identifier}, CorrelationId: {CorrelationId}",
                appConfig.AppId, identifier, correlationId);
            throw new ArgumentNullException(
                $"Authentication details are missing. AppId: {appConfig.AppId}, Identifier: {identifier}, CorrelationId: {correlationId}");
        }

        var integrationDetails = GetIntegrationDetails(appConfig);

        var storedProcedureName = integrationDetails.GetSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        var attribute = appConfig.UserAttributeSchemas.FirstOrDefault(a =>
                            a.SourceValue == "Identifier" && a.HttpRequestType == HttpRequestTypes.POST)
                        ?? throw new ArgumentException("Matching attribute not found for 'identifier'.");

        var parameters = new List<OdbcParameter>();
        var parameter = CreateOdbcParameter(GetParameterName(attribute),
            attribute.DestinationType.ToOdbcType(), attribute.DestinationTypeLength, identifier);
        parameters.Add(parameter);

        await using var connection = await OpenConnectionAsync(appConfig, correlationId, cancellationToken);

        var dbConn = DbConnectionFactory.Create(connection);
        await using var command = dbConn.CreateCommand(storedProcedureName, parameters, HttpRequestTypes.GET);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        // Advance to the first row; if no rows are returned, the user does not exist
        if (!await reader.ReadAsync(cancellationToken))
        {
            Log.Error(
                "No rows found for the given identifier. AppId: {AppId}, Identifier: {Identifier}, CorrelationId: {CorrelationId}",
                appConfig.AppId, identifier, correlationId);
            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }

        return new Core2EnterpriseUser
        {
            Identifier = GetUserInfoFromReader(reader, appConfig, "Identifier"),
            UserName = GetUserInfoFromReader(reader, appConfig, "UserName"),
        };
    }

    public virtual Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig,
        ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        // Action steps are not used by SQL integrations
        return GetAsync(identifier, appConfig, correlationId, cancellationToken);
    }

    /// <summary>
    /// Creates and opens the ODBC connection for the app. The caller owns (and must dispose) the connection.
    /// </summary>
    protected async Task<OdbcConnection> OpenConnectionAsync(AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken)
    {
        OdbcConnection connection =
            await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        Log.Information("DB Connection opened successfully. AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId, correlationId);

        return connection;
    }

    /// <summary>
    /// True for null and for a JSON null / undefined JsonElement (System.Text.Json snapshot values).
    /// </summary>
    private static bool IsMissing(object? value)
    {
        return value is null or System.Text.Json.JsonElement
        {
            ValueKind: System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined
        };
    }

    protected static SQLIntegrationDetails GetIntegrationDetails(AppConfig appConfig)
    {
        object? integrationDetailsJson = appConfig.IntegrationDetails;

        return JsonConvert.DeserializeObject<SQLIntegrationDetails>(integrationDetailsJson?.ToString()!)
               ?? throw new ArgumentException("Invalid integration details.");
    }

    /// <summary>
    /// SP parameter name of an attribute row (DestinationField without the URN prefix).
    /// </summary>
    protected static string GetParameterName(AttributeSchema attribute)
    {
        return attribute.DestinationField.Replace(UrnPrefix, string.Empty);
    }

    /// <summary>
    /// True when the row declares an SP OUTPUT parameter (SourceValue "Output" or "Output:&lt;Role&gt;").
    /// Evaluated before MappingType, so Constant and Direct markers behave identically.
    /// </summary>
    protected static bool IsOutputMapping(AttributeSchema attribute)
    {
        var sourceValue = attribute.SourceValue?.Trim();
        if (string.IsNullOrEmpty(sourceValue))
            return false;

        return sourceValue.Equals(OutputMarker, StringComparison.OrdinalIgnoreCase)
               || sourceValue.StartsWith(OutputMarker + ":", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Output parameters declared by output-marker rows, in mapping order.
    /// </summary>
    protected static IReadOnlyList<SqlOutputMapping> GetOutputMappings(IEnumerable<AttributeSchema> schema)
    {
        return schema.Where(IsOutputMapping).Select(ToOutputMapping).ToList();
    }

    private static SqlOutputMapping ToOutputMapping(AttributeSchema attribute)
    {
        var sourceValue = attribute.SourceValue.Trim();
        var role = sourceValue.Length > OutputMarker.Length
            ? sourceValue[(OutputMarker.Length + 1)..].Trim()
            : null;

        // Normalize the known roles to their canonical casing
        if (string.IsNullOrEmpty(role))
            role = null;
        else if (role.Equals(ResponseCodeRole, StringComparison.OrdinalIgnoreCase))
            role = ResponseCodeRole;
        else if (role.Equals(ResponseMessageRole, StringComparison.OrdinalIgnoreCase))
            role = ResponseMessageRole;

        return new SqlOutputMapping(GetParameterName(attribute), role, attribute.DestinationType,
            ToSqlDeclarationType(attribute.DestinationType));
    }

    /// <summary>
    /// Output-marker rows configured for the given request type. Operations without their own mapping rows
    /// (e.g. GET / DELETE, which schema detection does not cover) fall back to the POST rows.
    /// </summary>
    protected static IReadOnlyList<SqlOutputMapping> GetOutputMappings(AppConfig appConfig,
        HttpRequestTypes requestType)
    {
        var schema = appConfig.UserAttributeSchemas ?? [];

        var outputs = GetOutputMappings(schema.Where(a => a.HttpRequestType == requestType));
        if (outputs.Count == 0 && requestType != HttpRequestTypes.POST)
            outputs = GetOutputMappings(schema.Where(a => a.HttpRequestType == HttpRequestTypes.POST));

        return outputs;
    }

    #region Named stored procedure execution (SQL Server)

    // Format guards for identifiers written into the batch text; values are always bound as parameters.
    private static readonly Regex ParameterNamePattern = new(@"^@[A-Za-z0-9_]{1,127}$", RegexOptions.Compiled);
    private static readonly Regex ProcedureNamePattern = new(@"^[A-Za-z0-9_\.\[\]]{1,256}$", RegexOptions.Compiled);

    /// <summary>
    /// Executes a stored procedure with parameters bound by name and captures its OUTPUT parameters.
    /// Opt-in for derived integrations; the base CRUD methods keep the positional {CALL} behaviour.
    /// </summary>
    /// <param name="appConfig">App configuration (connection details)</param>
    /// <param name="procedureName">Stored procedure name, e.g. "dbo.usp_CreateUser"</param>
    /// <param name="inputs">Input parameters; ParameterName is the SP parameter name (e.g. "@LoginID")</param>
    /// <param name="outputs">OUTPUT parameters declared by output-marker rows</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    protected virtual async Task<SqlProcedureResult> ExecuteProcedureAsync(AppConfig appConfig, string procedureName,
        IReadOnlyList<OdbcParameter> inputs, IReadOnlyList<SqlOutputMapping> outputs, string correlationId,
        CancellationToken cancellationToken)
    {
        var commandText = BuildProcedureBatch(procedureName, inputs, outputs);

        await using var connection = await OpenConnectionAsync(appConfig, correlationId, cancellationToken);

        // The batch uses T-SQL (DECLARE / EXEC ... OUTPUT)
        if (!IsSqlServerDriver(connection.Driver))
        {
            throw new NotSupportedException(
                $"Named stored procedure execution requires a SQL Server ODBC driver. Driver '{connection.Driver}' is not supported.");
        }

        await using var command = new OdbcCommand(commandText, connection) { CommandType = CommandType.Text };
        command.Parameters.AddRange(inputs.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = await ReadProcedureResultAsync(reader, outputs, cancellationToken);

        Log.Information(
            "Stored procedure executed. StoredProcedure: {StoredProcedure}, ResponseCode: {ResponseCode}, AppId: {AppId}, CorrelationId: {CorrelationId}",
            procedureName, result.ResponseCode, appConfig.AppId, correlationId);

        return result;
    }

    /// <summary>
    /// Builds the parameterized T-SQL batch, e.g.
    /// <code>
    /// SET NOCOUNT ON;
    /// DECLARE @o0 INT, @o1 NVARCHAR(4000);
    /// EXEC dbo.usp_CreateUser @LoginID = ?, @Name = ?, @ResponseCode = @o0 OUTPUT, @ResponseMessage = @o1 OUTPUT;
    /// SELECT @o0 AS [o0], @o1 AS [o1];
    /// </code>
    /// Input values are bound to the '?' markers in the order of <paramref name="inputs"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Invalid procedure name, or an invalid / duplicate parameter name.</exception>
    protected static string BuildProcedureBatch(string procedureName, IReadOnlyList<OdbcParameter> inputs,
        IReadOnlyList<SqlOutputMapping> outputs)
    {
        if (string.IsNullOrWhiteSpace(procedureName) || !ProcedureNamePattern.IsMatch(procedureName))
            throw new ArgumentException($"Invalid stored procedure name '{procedureName}'.", nameof(procedureName));

        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in inputs.Select(p => p.ParameterName).Concat(outputs.Select(o => o.ParameterName)))
        {
            if (string.IsNullOrEmpty(name) || !ParameterNamePattern.IsMatch(name))
                throw new ArgumentException($"Invalid stored procedure parameter name '{name}' in DestinationField.");

            if (!parameterNames.Add(name))
                throw new ArgumentException($"Duplicate stored procedure parameter name '{name}' in DestinationField.");
        }

        var arguments = inputs.Select(p => $"{p.ParameterName} = ?")
            .Concat(outputs.Select((o, i) => $"{o.ParameterName} = @o{i} OUTPUT"))
            .ToList();

        var batch = new StringBuilder("SET NOCOUNT ON;\n");

        if (outputs.Count > 0)
            batch.Append("DECLARE ").AppendJoin(", ", outputs.Select((o, i) => $"@o{i} {o.SqlType}")).Append(";\n");

        batch.Append("EXEC ").Append(procedureName);
        if (arguments.Count > 0)
            batch.Append(' ').AppendJoin(", ", arguments);
        batch.Append(";\n");

        if (outputs.Count > 0)
            batch.Append("SELECT ").AppendJoin(", ", outputs.Select((_, i) => $"@o{i} AS [o{i}]")).Append(';');

        return batch.ToString();
    }

    /// <summary>
    /// Reads all result sets of a batch built by <see cref="BuildProcedureBatch"/>. With outputs, the last
    /// result set is the output row and the one before it (if any) is the procedure's row; without outputs,
    /// the last result set is the procedure's row.
    /// </summary>
    /// <exception cref="InvalidOperationException">Output row missing, or a mapped ResponseCode is NULL.</exception>
    protected static async Task<SqlProcedureResult> ReadProcedureResultAsync(DbDataReader reader,
        IReadOnlyList<SqlOutputMapping> outputs, CancellationToken cancellationToken)
    {
        // First row of every result set that has columns (null for an empty result set)
        var resultSets = new List<IReadOnlyDictionary<string, object?>?>();
        do
        {
            if (reader.FieldCount == 0)
                continue;

            resultSets.Add(await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null);

            while (await reader.ReadAsync(cancellationToken))
            {
                // Only the first row is used; drain the rest
            }
        } while (await reader.NextResultAsync(cancellationToken));

        var outputValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, object?>? row;

        if (outputs.Count > 0)
        {
            var outputRow = resultSets.Count > 0 ? resultSets[^1] : null;
            if (outputRow == null)
                throw new InvalidOperationException("Stored procedure returned no output values.");

            for (var i = 0; i < outputs.Count; i++)
                outputValues[outputs[i].ParameterName] = outputRow.GetValueOrDefault($"o{i}");

            row = resultSets.Count > 1 ? resultSets[^2] : null;
        }
        else
        {
            row = resultSets.Count > 0 ? resultSets[^1] : null;
        }

        int? responseCode = null;
        var responseCodeOutput = outputs.FirstOrDefault(o => o.Role == ResponseCodeRole);
        if (responseCodeOutput != null)
        {
            var value = outputValues[responseCodeOutput.ParameterName]
                        ?? throw new InvalidOperationException("Stored procedure returned no response code.");
            responseCode = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        string? responseMessage = null;
        var responseMessageOutput = outputs.FirstOrDefault(o => o.Role == ResponseMessageRole);
        if (responseMessageOutput != null)
            responseMessage = Convert.ToString(outputValues[responseMessageOutput.ParameterName], CultureInfo.InvariantCulture);

        return new SqlProcedureResult(responseCode, responseMessage, row, outputValues);
    }

    /// <summary>
    /// Throws an <see cref="HttpResponseException"/> when the procedure reported a non-success ResponseCode.
    /// No-op when no ResponseCode output is mapped.
    /// </summary>
    protected virtual void ThrowIfFailed(SqlProcedureResult result, string procedureName, AppConfig appConfig,
        string correlationId)
    {
        if (result.ResponseCode is null or (int)SqlProcedureResponseCode.Success)
            return;

        var statusCode = MapResponseCode(result.ResponseCode.Value);

        Log.Error(
            "Stored procedure reported failure. StoredProcedure: {StoredProcedure}, ResponseCode: {ResponseCode}, ResponseMessage: {ResponseMessage}, HttpStatus: {HttpStatus}, AppId: {AppId}, CorrelationId: {CorrelationId}",
            procedureName, result.ResponseCode, result.ResponseMessage, (int)statusCode, appConfig.AppId,
            correlationId);

        throw new HttpResponseException(statusCode);
    }

    /// <summary>
    /// Maps a non-success ResponseCode to an HTTP status. Override for procedures with another convention.
    /// </summary>
    protected virtual HttpStatusCode MapResponseCode(int responseCode)
    {
        return (SqlProcedureResponseCode)responseCode switch
        {
            SqlProcedureResponseCode.ValidationError => HttpStatusCode.BadRequest,
            SqlProcedureResponseCode.NotFound => HttpStatusCode.NotFound,
            SqlProcedureResponseCode.Conflict => HttpStatusCode.Conflict,
            _ => HttpStatusCode.InternalServerError
        };
    }

    private static Dictionary<string, object?> ReadRow(DbDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            if (string.IsNullOrEmpty(name))
                continue; // Unaliased expression column

            row[name] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static bool IsSqlServerDriver(string driver)
    {
        var driverName = driver.ToUpperInvariant();

        // Linux / macOS driver files carry a version suffix, e.g. libmsodbcsql-17.10.so.6.1
        return SQLGlobalConstants.SqlServerDrivers.Contains(driverName)
               || driverName.StartsWith("LIBMSODBCSQL", StringComparison.Ordinal)
               || driverName.StartsWith("MSODBCSQL", StringComparison.Ordinal);
    }

    #endregion

    /// <summary>
    /// T-SQL type used to DECLARE the local variable that receives an OUTPUT parameter.
    /// </summary>
    protected static string ToSqlDeclarationType(AttributeDataTypes dataType)
    {
        return dataType switch
        {
            AttributeDataTypes.String or AttributeDataTypes.NVarChar or AttributeDataTypes.VarChar
                or AttributeDataTypes.Char or AttributeDataTypes.NChar or AttributeDataTypes.Text
                or AttributeDataTypes.NText or AttributeDataTypes.Array or AttributeDataTypes.Object
                or AttributeDataTypes.Null => "NVARCHAR(4000)",
            AttributeDataTypes.Int => "INT",
            AttributeDataTypes.Number or AttributeDataTypes.BigInt => "BIGINT",
            AttributeDataTypes.SmallInt => "SMALLINT",
            AttributeDataTypes.TinyInt => "TINYINT",
            AttributeDataTypes.Boolean or AttributeDataTypes.Bit => "BIT",
            AttributeDataTypes.DateTime or AttributeDataTypes.SmallDateTime
                or AttributeDataTypes.Timestamp => "DATETIME2",
            AttributeDataTypes.Date => "DATE",
            AttributeDataTypes.Time => "TIME",
            AttributeDataTypes.Decimal or AttributeDataTypes.Numeric => "DECIMAL(18,2)",
            AttributeDataTypes.Double => "FLOAT",
            AttributeDataTypes.Real => "REAL",
            AttributeDataTypes.UniqueIdentifier => "UNIQUEIDENTIFIER",
            _ => throw new NotSupportedException($"Data type '{dataType}' is not supported for SQL output parameters.")
        };
    }

    private static void ValidateAttributeSchema(IList<AttributeSchema> schema)
    {
        if (schema == null || !schema.Any())
        {
            Log.Error("Attribute schema is empty.");
            throw new HttpRequestException("Attribute schema is empty.");
        }

        if (schema.Any(x => string.IsNullOrEmpty(x.SourceValue) || string.IsNullOrEmpty(x.DestinationField)))
        {
            Log.Error("Source value or destination field is empty in the attribute schema.");
            throw new HttpRequestException("Source value or destination field is empty.");
        }
    }

    private string GetUserInfoFromReader(DbDataReader reader, AppConfig appConfig, string attribute)
    {
        var reqAttribute = appConfig.UserAttributeSchemas.FirstOrDefault(a => a.SourceValue == attribute)
                           ?? throw new HttpRequestException("Matching attribute not found for 'identifier'.");

        var columnIndex =
            reader.GetOrdinal(Regex.Replace(GetParameterName(reqAttribute), @"[^a-zA-Z0-9]", ""));
        if (columnIndex < 0)
        {
            Log.Error("Expected column '{Column}' not found in the result set. AppId: {AppId}",
                GetParameterName(reqAttribute), appConfig.AppId);
            throw new HttpRequestException(
                $"Expected column '{GetParameterName(reqAttribute)}' not found in the result set.");
        }

        // Handle null or DBNull values gracefully
        return !reader.IsDBNull(columnIndex) ? reader.GetString(columnIndex) : string.Empty;
    }
}
