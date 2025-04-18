using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json;
using System.Data.Common;
using System.Data.Odbc;
using System.Text.RegularExpressions;
using System.Web.Http;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class SQLIntegration : IIntegrationBase
{
    public IntegrationMethods IntegrationMethod { get; init; }
    private readonly IOptions<AppSettings> _appSettings;
    string urnPrefix = "urn:kn:ki:schema:";

    public SQLIntegration(IOptions<AppSettings> appSettings)
    {
        IntegrationMethod = IntegrationMethods.SQL;
        _appSettings = appSettings;
    }

    public async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default)
    {
        if (config.AuthenticationDetails == null)
        {
            Log.Error("Authentication details are missing. AppId: {AppId}", config.AppId);
            throw new ArgumentNullException("Authentication details are missing.");
        }

        if (config.AuthenticationDetails == null || config.AuthenticationDetails?.Driver == null ||
            config.AuthenticationDetails?.Database == null
            || config.AuthenticationDetails?.Server == null || config.AuthenticationDetails?.UID == null ||
            config.AuthenticationDetails?.PWD == null)
        {
            Log.Error("Invalid authentication details. AppId: {AppId}", config.AppId);
            throw new ArgumentNullException("Invalid authentication details.");
        }
        else
        {
            var authenticationDetails =
                JsonConvert.DeserializeObject<SQLAuthentication>(config.AuthenticationDetails?.ToString())
                ?? throw new ArgumentException("Invalid authentication details.");

            string connectionString = DatabaseConnectionUtil.GetConnectionString(authenticationDetails);

            var connection = new OdbcConnection(connectionString);
            return await Task.FromResult(connection);
        }
    }

    public async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        ValidateAttributeSchema(schema);

        if (resource == null)
            throw new ArgumentNullException("Invalid resource");

        var parameters = new List<OdbcParameter>();

        foreach (var attribute in schema)
        {
            dynamic? value = null;

            if (attribute.MappingType == MappingTypes.Direct)
            {
                value = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, attribute) ?? DBNull.Value;
            }
            else if (attribute.MappingType == MappingTypes.Constant)
            {
                value = attribute.SourceValue != null ? attribute.SourceValue : DBNull.Value;
            }

            var parameter = CreateOdbcParameter(attribute.DestinationField.Replace(urnPrefix, string.Empty),
                attribute.DestinationType.ToOdbcType(), attribute.DestinationTypeLength, value);
            parameters.Add(parameter);
        }

        // Return the prepared SQL Parameter
        return await Task.FromResult(parameters);
    }

    private OdbcParameter CreateOdbcParameter(string destinationField, OdbcType destinationType,
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

    public async Task ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
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

        var integrationDetails =
            JsonConvert.DeserializeObject<SQLIntegrationDetails>(appConfig.IntegrationDetails?.ToString())
            ?? throw new ArgumentException("Invalid authentication details.");

        var storedProcedureName = integrationDetails.PostSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        // Get the database connection using GetAuthenticationAsync
        var connection =
            (await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken) as OdbcConnection)!;
        // Open the connection here
        await connection.OpenAsync(cancellationToken);

        var dbConn = DbConnectionFactory.Create(connection);
        var command = dbConn.CreateCommand(storedProcedureName, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await connection.CloseAsync();
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, Array.Empty<string>()));
    }

    public Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotSupportedException("Replace operation not supported for SQL Integration");
    }

    public async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
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

        var integrationDetails =
            JsonConvert.DeserializeObject<SQLIntegrationDetails>(appConfig.IntegrationDetails?.ToString())
            ?? throw new ArgumentException("Invalid authentication details.");

        var storedProcedureName = integrationDetails.PatchSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        // Get the database connection using GetAuthenticationAsync
        var connection = (await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound) as OdbcConnection)!;
        // Open the connection here
        await connection.OpenAsync();

        Log.Warning("DB Connection opened successfully. AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId,
            correlationId);

        var dbConn = DbConnectionFactory.Create(connection);
        var command = dbConn.CreateCommand(storedProcedureName, parameters);
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
        Log.Information(
            "Update command executed successfully. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
            correlationId);
    }

    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Log.Error("Identifier is null or invalid. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
                correlationId);
            throw new ArgumentNullException(
                $"Identifier is null or invalid. AppId: {appConfig.AppId}, CorrelationId: {correlationId}");
        }

        var integrationDetails =
            JsonConvert.DeserializeObject<SQLIntegrationDetails>(appConfig.IntegrationDetails?.ToString())
            ?? throw new ArgumentException("Invalid authentication details.");

        var storedProcedureName = integrationDetails.DeleteSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        var attribute = appConfig.UserAttributeSchemas.FirstOrDefault(a => a.SourceValue == "Identifier")
                        ?? throw new ArgumentException("Matching attribute not found for 'identifier'.");

        var parameters = new List<OdbcParameter>();
        var parameter = CreateOdbcParameter(attribute.DestinationField.Replace(urnPrefix, string.Empty),
            attribute.DestinationType.ToOdbcType(), attribute.DestinationTypeLength, identifier);
        parameters.Add(parameter);

        // Get the database connection using GetAuthenticationAsync
        var connection = (await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound) as OdbcConnection)!;

        // Open the connection here
        await connection.OpenAsync();
        Log.Warning("DB Connection opened successfully. AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId,
            correlationId);

        var dbConn = DbConnectionFactory.Create(connection);
        var command = dbConn.CreateCommand(storedProcedureName, parameters);
        await command.ExecuteNonQueryAsync();

        Log.Information(
            "Delete command executed successfully. Identifier: {Identifier}, AppId: {AppId}, CorrelationId: {CorrelationId}",
            identifier, appConfig.AppId, correlationId);
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            Log.Error("Identifier is null or invalid. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
                correlationId);
            throw new ArgumentNullException(
                $"Identifier is null or Invalid. AppId: {appConfig.AppId}, Identifier: {identifier}, CorrelationId: {correlationId}");
        }

        if (appConfig.IntegrationDetails == null)
        {
            Log.Error(
                "Integration details are missing. AppId: {AppId}, Identifier: {Identifier}, CorrelationId: {CorrelationId}",
                appConfig.AppId, identifier, correlationId);
            throw new ArgumentNullException(
                $"Authentication details are missing. AppId: {appConfig.AppId}, Identifier: {identifier}, CorrelationId: {correlationId}");
        }

        var integrationDetails =
            JsonConvert.DeserializeObject<SQLIntegrationDetails>(appConfig.IntegrationDetails?.ToString())
            ?? throw new ArgumentException("Invalid IntegrationDetails details.");


        var storedProcedureName = integrationDetails.GetSpName
                                  ?? throw new ArgumentException("Provisioning details are missing.");

        var attribute = appConfig.UserAttributeSchemas.FirstOrDefault(a =>
                            a.SourceValue == "Identifier" && a.HttpRequestType == HttpRequestTypes.POST)
                        ?? throw new ArgumentException("Matching attribute not found for 'identifier'.");

        var parameters = new List<OdbcParameter>();
        var parameter = CreateOdbcParameter(attribute.DestinationField.Replace(urnPrefix, string.Empty),
            attribute.DestinationType.ToOdbcType(), attribute.DestinationTypeLength, identifier);
        parameters.Add(parameter);

        // Get the database connection using GetAuthenticationAsync
        var connection =
            (await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken) as OdbcConnection)!;
        // Open the connection here
        await connection.OpenAsync(cancellationToken);

        var dbConn = DbConnectionFactory.Create(connection);
        var command = dbConn.CreateCommand(storedProcedureName, parameters, HttpRequestTypes.GET);
        var reader = await command.ExecuteReaderAsync(cancellationToken);

        // If no rows are returned, throw an exception
        if (!reader.HasRows)
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

    private static void ValidateAttributeSchema(IList<AttributeSchema> schema)
    {
        if (schema == null || !schema.Any())
        {
            Log.Error("Attribute schema is empty.");
            throw new HttpRequestException("Attribute schema is empty.");
        }

        if (schema.Any(x => string.IsNullOrEmpty(x.SourceValue) || string.IsNullOrEmpty(x.DestinationField)))
            throw new HttpRequestException("Source value or destination field is empty.");
    }

    private string GetUserInfoFromReader(DbDataReader reader, AppConfig appConfig, string attribute)
    {
        var reqAttribute = appConfig.UserAttributeSchemas.FirstOrDefault(a => a.SourceValue == attribute)
                           ?? throw new HttpRequestException("Matching attribute not found for 'identifier'.");

        var columnIndex =
            reader.GetOrdinal(Regex.Replace(reqAttribute.DestinationField.Replace(urnPrefix, string.Empty),
                @"[^a-zA-Z0-9]", ""));
        if (columnIndex < 0)
            throw new HttpRequestException(
                $"Expected column '{reqAttribute.DestinationField.Replace(urnPrefix, string.Empty)}' not found in the result set.");

        // Handle null or DBNull values gracefully
        return !reader.IsDBNull(columnIndex) ? reader.GetString(columnIndex) : string.Empty;
    }
}