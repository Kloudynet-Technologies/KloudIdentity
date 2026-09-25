using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Serilog;
using System.Data.Odbc;
using System.Globalization;
using System.Net;
using System.Web.Http;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// PNB UTS Archival (database LoginArchival) provisioning through the usp_*UTSArchivalUser stored procedures.
/// The procedures never raise errors; every outcome is reported through the OUTPUT parameters mapped as
/// "Output:ResponseCode" / "Output:ResponseMessage". Uses the named stored procedure engine of
/// <see cref="SQLIntegration"/>; this class only adds the UTS rules. Routed by AppId
/// (IntegrationMappings.AppIdToIntegration).
/// </summary>
public class UTSArchivalSQLIntegration : SQLIntegration
{
    /// <summary>
    /// Status parameter of usp_UpdateUTSArchivalUser. Never mapped: set by code (current Flag when Create
    /// finds an existing user; derived from "active" on Update).
    /// </summary>
    private const string FlagParameter = "@Flag";

    // @Flag values accepted by usp_UpdateUTSArchivalUser (1 Active, 2 Disable, 3 Inactive)
    private static readonly HashSet<int> SettableFlags = [1, 2, 3];
    private const int ActiveFlag = 1;
    private const int DisableFlag = 2;

    private readonly IPatchOperationContext _patchOperationContext;

    public UTSArchivalSQLIntegration(IOptions<AppSettings> appSettings, IPatchOperationContext patchOperationContext)
        : base(appSettings)
    {
        IntegrationMethod = IntegrationMethods.SQL;
        _patchOperationContext = patchOperationContext;
    }

    #region Mapping

    public override Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<dynamic>(MapInputs(schema, resource, appId: null));
    }

    /// <summary>
    /// Maps the operation's rows (CreateUserV4 passes the POST rows, UpdateUserV4 the PATCH rows) to named
    /// input parameters. Unmapped detection rows and output markers are skipped.
    /// For Update (PATCH rows) PATCH semantics apply, see <see cref="MapUpdateInputs"/>.
    /// For Create (POST rows) the payload also keeps the Entra user, because ProvisionAsync only receives the
    /// payload: if GetSpName then finds the user, the PATCH rows are mapped from it for the Update call
    /// (see <see cref="ProvisionAsync(object, AppConfig, string, CancellationToken)"/>). Nothing is mapped for
    /// Update unless the user exists.
    /// </summary>
    public override Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource, AppConfig appConfig, CancellationToken cancellationToken = default)
    {
        // ReplaceUserV4 passes the PUT rows; the UTS app has none (PUT reuses the PATCH rows, see ReplaceAsync).
        // An empty payload is validated by the operation that receives it.
        if (schema is null || schema.Count == 0)
            return Task.FromResult<dynamic>(new UpdatePayload());

        if (schema.Any(a => a.HttpRequestType != HttpRequestTypes.POST))
            return Task.FromResult<dynamic>(MapUpdateInputs(schema, resource, appConfig));

        var inputs = MapInputs(schema, resource, appConfig.AppId);
        return Task.FromResult<dynamic>(new ProvisionPayload(inputs) { Resource = resource });
    }

    /// <summary>
    /// UTS rule on top of the base conversion: a Direct integer value of 0 is the default of an unset value
    /// (e.g. no role) and is sent as NULL, so the SP validates it (e.g. @LevelsID → ResponseCode 1).
    /// Constant integers (e.g. @CreatedBy = 0) are sent as-is.
    /// </summary>
    protected override object ConvertInputValue(AttributeSchema attribute, object? rawValue)
    {
        var value = base.ConvertInputValue(attribute, rawValue);

        return attribute.MappingType != MappingTypes.Constant && value is 0 or 0L or (short)0 or (byte)0
            ? DBNull.Value
            : value;
    }

    private List<OdbcParameter> MapInputs(IList<AttributeSchema> schema, Core2EnterpriseUser resource, string? appId)
    {
        if (schema == null || schema.Count == 0)
            throw new InvalidOperationException($"No attribute mapping configured for AppId {appId}.");

        var parameters = MapProcedureInputs(schema, resource);
        if (parameters.Count == 0)
            throw new InvalidOperationException($"No mapped stored procedure parameters configured for AppId {appId}.");

        return parameters;
    }

    /// <summary>
    /// Update inputs for a Create whose user GetSpName has found: the app's PATCH rows mapped from the incoming
    /// Entra user. The lookup parameter and @Flag (the existing Flag) are added by the caller; a PATCH mapping
    /// to either is ignored. Empty when the app has no mapped PATCH rows (link only).
    /// </summary>
    private List<OdbcParameter> MapExistingUserUpdateInputs(AppConfig appConfig, Core2EnterpriseUser resource)
    {
        var patchRows = appConfig.UserAttributeSchemas?
            .Where(a => a.HttpRequestType == HttpRequestTypes.PATCH)
            .ToList() ?? [];

        var lookupParameter = ResolveLookupParameter(appConfig);
        return MapProcedureInputs(patchRows, resource)
            .Where(p => !IsParameter(p, lookupParameter) && !IsParameter(p, FlagParameter))
            .ToList();
    }

    /// <summary>
    /// Update inputs with PATCH semantics (UpdateUserV4 applies the PATCH to an empty user, so every attribute
    /// that is not in the PATCH looks empty):
    /// <list type="bullet">
    /// <item>Direct row whose attribute is not in the PATCH → DBNull (the SP treats NULL as "unchanged").</item>
    /// <item>Direct row whose attribute is in the PATCH but empty / removed → '' for strings, so the value is
    /// cleared; non-string values cannot be cleared through the UTS SPs (NULL = unchanged) → DBNull + warning.</item>
    /// <item>Constant rows (e.g. @UpdatedBy) are always sent.</item>
    /// <item>The lookup parameter and @Flag are added in <see cref="UpdateAsync(object, Core2EnterpriseUser, AppConfig, string)"/>;
    /// a PATCH mapping to either is ignored.</item>
    /// </list>
    /// Without a captured PATCH (not called through UpdateUserV4) values are sent as mapped and nothing is cleared.
    /// </summary>
    private UpdatePayload MapUpdateInputs(IList<AttributeSchema> schema, Core2EnterpriseUser resource,
        AppConfig appConfig)
    {
        var lookupParameter = ResolveLookupParameter(appConfig);
        var patchCaptured = _patchOperationContext.IsCaptured;
        var payload = new UpdatePayload();

        foreach (var attribute in schema.Where(IsProcedureInputRow))
        {
            var parameterName = GetParameterName(attribute);
            if (string.Equals(parameterName, lookupParameter, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parameterName, FlagParameter, StringComparison.OrdinalIgnoreCase))
                continue;

            // Always resolved, so a SourceValue that does not exist on the user fails loudly (config error)
            var value = MapInputValue(attribute, resource, applyDefault: false);

            if (attribute.MappingType != MappingTypes.Constant && patchCaptured)
            {
                if (!_patchOperationContext.IsPatched(attribute.SourceValue))
                {
                    value = DBNull.Value;
                }
                else if (value is DBNull)
                {
                    if (IsStringType(attribute.DestinationType))
                        value = string.Empty;
                    else
                        Log.Warning("UTS update: {Parameter} was removed in the PATCH, but it cannot be cleared through the SP; left unchanged. AppId: {AppId}",
                            parameterName, appConfig.AppId);
                }
            }

            if (attribute.MappingType != MappingTypes.Constant && value is not DBNull)
                payload.HasChanges = true;

            payload.Add(CreateInputParameter(attribute, value));
        }

        if (payload.Count == 0)
            throw new InvalidOperationException($"No mapped stored procedure parameters configured for AppId {appConfig.AppId}.");

        return payload;
    }

    /// <summary>
    /// Update payload: the PATCH inputs; HasChanges is false when no mapped attribute has a value to write.
    /// </summary>
    private sealed class UpdatePayload : List<OdbcParameter>
    {
        public bool HasChanges { get; set; }
    }

    private static bool IsParameter(OdbcParameter parameter, string name)
    {
        return string.Equals(parameter.ParameterName, name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Create payload: the POST inputs plus the Entra user, used to map the PATCH rows only when GetSpName
    /// finds that the user already exists.
    /// </summary>
    private sealed class ProvisionPayload(IEnumerable<OdbcParameter> createInputs) : List<OdbcParameter>(createInputs)
    {
        public Core2EnterpriseUser? Resource { get; init; }
    }

    #endregion

    #region Create

    /// <summary>
    /// Creates the user through PostSpName. Success is ResponseCode 0 only; the returned SCIM Identifier is the
    /// LoginID (never a GUID), so later Get / Update / Delete calls receive the LoginID.
    /// The user is first looked up through GetSpName: if the LoginID already exists (e.g. accounts created
    /// before provisioning), the PATCH-mapped attributes are applied through PatchSpName instead of creating,
    /// and the existing LoginID is returned. The account status is kept: @Flag is the Flag returned by GetSpName.
    /// </summary>
    public override async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig,
        string correlationId, CancellationToken cancellationToken = default)
    {
        var inputs = payload as List<OdbcParameter>;
        if (inputs is null || inputs.Count == 0)
            throw new ArgumentNullException(nameof(payload), "No valid SqlParameter found in the provided payload.");

        var procedureName = GetIntegrationDetails(appConfig).PostSpName;
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException($"PostSpName is not configured for AppId {appConfig.AppId}.");

        // Resolve configuration before touching the database
        var outputs = ResolveStatusOutputs(appConfig, HttpRequestTypes.POST);
        var lookupParameter = ResolveLookupParameter(appConfig);

        var requestedLoginId = ReadSentValue(inputs, lookupParameter);
        if (requestedLoginId is not null)
        {
            var existingUser = await FindUserRowAsync(appConfig, requestedLoginId, correlationId, cancellationToken);
            if (existingUser is not null)
            {
                return await UpdateExistingUserAsync(appConfig, requestedLoginId, existingUser,
                    (inputs as ProvisionPayload)?.Resource, correlationId, cancellationToken);
            }
        }

        Log.Information("UTS create started. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
            correlationId);

        SqlProcedureResult result =
            await ExecuteProcedureAsync(appConfig, procedureName, inputs, outputs, correlationId, cancellationToken);
        ThrowIfFailed(result, procedureName, appConfig, correlationId);

        var loginId = ResolveCreatedIdentifier(result, inputs, lookupParameter)
                      ?? throw new InvalidOperationException(
                          $"UTS create succeeded but no {lookupParameter} was returned or sent. AppId: {appConfig.AppId}");

        Log.Information("UTS create completed. AppId: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId,
            correlationId);

        return new Core2EnterpriseUser { Identifier = loginId, UserName = loginId };
    }

    /// <summary>
    /// LoginID of the created user: the lookup column of the returned row (e.g. "LoginID" for "@LoginID"),
    /// falling back to the value sent for the lookup parameter.
    /// </summary>
    private static string? ResolveCreatedIdentifier(SqlProcedureResult result, IEnumerable<OdbcParameter> inputs,
        string lookupParameter)
    {
        var column = lookupParameter.TrimStart('@');
        if (result.Row?.GetValueOrDefault(column)?.ToString() is { } returned && !string.IsNullOrWhiteSpace(returned))
            return returned.Trim();

        return ReadSentValue(inputs, lookupParameter);
    }

    private static string? ReadSentValue(IEnumerable<OdbcParameter> inputs, string parameterName)
    {
        var sent = inputs.FirstOrDefault(p => IsParameter(p, parameterName))?.Value;

        return sent is null or DBNull || string.IsNullOrWhiteSpace(sent.ToString()) ? null : sent.ToString()!.Trim();
    }

    /// <summary>
    /// Create for a LoginID that GetSpName has found: maps the PATCH rows from the Entra user, applies them
    /// through PatchSpName (link only when the app has no PATCH mapping) and returns the existing LoginID as
    /// the SCIM Identifier.
    /// </summary>
    private async Task<Core2EnterpriseUser> UpdateExistingUserAsync(AppConfig appConfig, string loginId,
        IReadOnlyDictionary<string, object?> existingUser, Core2EnterpriseUser? resource, string correlationId,
        CancellationToken cancellationToken)
    {
        var identifier = ReadString(existingUser, UserIdColumn) ?? loginId;

        // Mapped only now that the user is known to exist
        var updateInputs = resource is null ? [] : MapExistingUserUpdateInputs(appConfig, resource);
        if (updateInputs.Count == 0)
        {
            Log.Information(
                "UTS create: user already exists, linked without update (no PATCH mapping). AppId: {AppId}, CorrelationId: {CorrelationId}",
                appConfig.AppId, correlationId);
            return new Core2EnterpriseUser { Identifier = identifier, UserName = identifier };
        }

        var procedureName = GetIntegrationDetails(appConfig).PatchSpName;
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException($"PatchSpName is not configured for AppId {appConfig.AppId}.");

        var outputs = ResolveStatusOutputs(appConfig, HttpRequestTypes.PATCH);
        var inputs = new List<OdbcParameter> { CreateLookupParameter(appConfig, loginId) };
        inputs.AddRange(updateInputs);

        // Keep the account status: send the Flag returned by GetSpName. usp_UpdateUTSArchivalUser only accepts
        // 1 / 2 / 3; for the password states (4 / 98 / 99) @Flag is omitted, which also leaves the Flag unchanged.
        if (ReadInt(existingUser, FlagColumn) is { } currentFlag && SettableFlags.Contains(currentFlag))
            inputs.Add(CreateOdbcParameter(FlagParameter, OdbcType.Int, null, currentFlag));

        Log.Information(
            "UTS create: user already exists, updating instead of creating (status unchanged). AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId, correlationId);

        var result =
            await ExecuteProcedureAsync(appConfig, procedureName, inputs, outputs, correlationId, cancellationToken);
        ThrowIfFailed(result, procedureName, appConfig, correlationId);

        return new Core2EnterpriseUser { Identifier = identifier, UserName = identifier };
    }

    #endregion

    #region Update

    /// <summary>
    /// Applies a PATCH through PatchSpName by LoginID (the SCIM Identifier). Only patched attributes are sent
    /// (see <see cref="MapUpdateInputs"/>). The status follows "active" only when it is explicitly set in the PATCH
    /// (Entra sends disable and unassign as PATCH active=false):
    /// <list type="bullet">
    /// <item>active = false → @Flag 2 (Disable; the SP also clears IsLogin).</item>
    /// <item>active = true → @Flag 1 only when the current Flag is 2 / 3 (the SP reactivates to Flag 4 with a
    /// password reset). For an account that is already active nothing is sent: @Flag 1 on Flag 99 / 4 / 98 would
    /// skip the forced password change (known SP defect).</item>
    /// <item>active not in the PATCH → @Flag not sent (status unchanged).</item>
    /// </list>
    /// When nothing remains to change, the SP is not called.
    /// </summary>
    public override async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var inputs = payload as List<OdbcParameter>
                     ?? throw new ArgumentNullException(nameof(payload), "No valid SqlParameter found in the provided payload.");
        if (inputs.Count == 0)
            throw new InvalidOperationException($"No mapped PATCH parameters configured for AppId {appConfig.AppId}.");

        var identifier = resource?.Identifier;
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentNullException(nameof(resource), $"Identifier is null or empty. AppId: {appConfig.AppId}");

        var procedureName = GetIntegrationDetails(appConfig).PatchSpName;
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException($"PatchSpName is not configured for AppId {appConfig.AppId}.");

        var outputs = ResolveStatusOutputs(appConfig, HttpRequestTypes.PATCH);
        int? flag = await ResolveFlagAsync(resource!, identifier, appConfig, correlationId);

        var hasChanges = inputs is UpdatePayload updatePayload
            ? updatePayload.HasChanges
            : inputs.Any(p => p.Value is not DBNull);

        if (!hasChanges && flag is null)
        {
            Log.Information("UTS update: nothing to change; SP not called. AppId: {AppId}, CorrelationId: {CorrelationId}",
                appConfig.AppId, correlationId);
            return;
        }

        var parameters = new List<OdbcParameter> { CreateLookupParameter(appConfig, identifier) };
        parameters.AddRange(inputs);
        if (flag is not null)
            parameters.Add(CreateOdbcParameter(FlagParameter, OdbcType.Int, null, flag.Value));

        var result = await ExecuteProcedureAsync(appConfig, procedureName, parameters, outputs, correlationId,
            CancellationToken.None);
        ThrowIfFailed(result, procedureName, appConfig, correlationId);

        Log.Information("UTS update completed. StatusChanged: {StatusChanged}, AppId: {AppId}, CorrelationId: {CorrelationId}",
            flag is not null, appConfig.AppId, correlationId);
    }

    /// <summary>
    /// @Flag for an Update, or null to leave the status unchanged.
    /// </summary>
    private async Task<int?> ResolveFlagAsync(Core2EnterpriseUser resource, string identifier, AppConfig appConfig,
        string correlationId)
    {
        if (!_patchOperationContext.IsCaptured || !_patchOperationContext.IsSet(nameof(Core2EnterpriseUser.Active)))
            return null;

        return await ResolveActiveFlagAsync(resource.Active, identifier, appConfig, correlationId);
    }

    /// <summary>
    /// @Flag for an explicit active value: false → 2; true → 1 when the account is Flag 2 / 3 (the SP then
    /// reactivates to Flag 4), otherwise null (already active; @Flag 1 on 99 / 4 / 98 would skip the forced
    /// password change).
    /// </summary>
    private async Task<int?> ResolveActiveFlagAsync(bool active, string identifier, AppConfig appConfig,
        string correlationId)
    {
        if (!active)
            return DisableFlag;

        var existingUser = await FindUserRowAsync(appConfig, identifier, correlationId, CancellationToken.None)
                           ?? throw new HttpResponseException(HttpStatusCode.NotFound);

        if (ReadInt(existingUser, FlagColumn) is { } currentFlag && InactiveFlags.Contains(currentFlag))
            return ActiveFlag;

        Log.Information("UTS update: active = true and the account is already active; status unchanged. AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId, correlationId);
        return null;
    }

    #endregion

    #region Replace (PUT)

    /// <summary>
    /// PUT replaces the whole user: handled as a full update through PatchSpName (the same SP as PATCH).
    /// Uses the PUT rows when the app has them, otherwise the PATCH rows mapped from the full user in the body
    /// (missing values → NULL = unchanged). Status follows "active" in the body: false → @Flag 2, true → @Flag 1
    /// only for a Flag 2 / 3 account. A PUT body without "active" is read as false (SCIM default) and disables.
    /// </summary>
    public override Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        return ReplaceUserAsync(payload as List<OdbcParameter>, resource, appConfig, correlationId);
    }

    public override async Task<Core2EnterpriseUser> ReplaceAsync(dynamic payload, Core2EnterpriseUser resource,
        string appId, AppConfig appConfig, ActionStep actionStep, string correlationId,
        CancellationToken cancellationToken = default)
    {
        await ReplaceUserAsync(payload as List<OdbcParameter>, resource, appConfig, correlationId);
        return resource;
    }

    private async Task ReplaceUserAsync(List<OdbcParameter>? payloadInputs, Core2EnterpriseUser resource,
        AppConfig appConfig, string correlationId)
    {
        var identifier = resource?.Identifier;
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentNullException(nameof(resource), $"Identifier is null or empty. AppId: {appConfig.AppId}");

        var procedureName = GetIntegrationDetails(appConfig).PatchSpName;
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException($"PatchSpName is not configured for AppId {appConfig.AppId}.");

        var inputs = payloadInputs is { Count: > 0 } ? payloadInputs : MapExistingUserUpdateInputs(appConfig, resource!);
        if (inputs.Count == 0)
            throw new InvalidOperationException($"No mapped PATCH parameters configured for AppId {appConfig.AppId}.");

        var outputs = ResolveStatusOutputs(appConfig, HttpRequestTypes.PATCH);
        var flag = await ResolveActiveFlagAsync(resource!.Active, identifier, appConfig, correlationId);

        var parameters = new List<OdbcParameter> { CreateLookupParameter(appConfig, identifier) };
        parameters.AddRange(inputs.Where(p => !IsParameter(p, FlagParameter)));
        if (flag is not null)
            parameters.Add(CreateOdbcParameter(FlagParameter, OdbcType.Int, null, flag.Value));

        var result = await ExecuteProcedureAsync(appConfig, procedureName, parameters, outputs, correlationId,
            CancellationToken.None);
        ThrowIfFailed(result, procedureName, appConfig, correlationId);

        Log.Information("UTS replace completed. StatusChanged: {StatusChanged}, AppId: {AppId}, CorrelationId: {CorrelationId}",
            flag is not null, appConfig.AppId, correlationId);
    }

    #endregion

    #region Delete

    /// <summary>
    /// Disables the user through DeleteSpName by LoginID (UTS never hard-deletes: Flag → 2). ResponseCode 3
    /// ("already disabled") is treated as success so a repeated DELETE is idempotent; 2 (not found) → 404,
    /// which Entra accepts for DELETE. @DeletedBy is not sent (SP default 0).
    /// </summary>
    public override async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        // DeleteUserV4 does not validate the identifier for SQL apps
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentNullException(nameof(identifier), $"Identifier is null or empty. AppId: {appConfig.AppId}");

        var procedureName = GetIntegrationDetails(appConfig).DeleteSpName;
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException($"DeleteSpName is not configured for AppId {appConfig.AppId}.");

        var outputs = ResolveStatusOutputs(appConfig, HttpRequestTypes.DELETE);
        var inputs = new List<OdbcParameter> { CreateLookupParameter(appConfig, identifier) };

        var result =
            await ExecuteProcedureAsync(appConfig, procedureName, inputs, outputs, correlationId, CancellationToken.None);

        if (result.ResponseCode == (int)SqlProcedureResponseCode.Conflict)
        {
            Log.Information("UTS delete: user is already disabled; treated as success. AppId: {AppId}, CorrelationId: {CorrelationId}",
                appConfig.AppId, correlationId);
            return;
        }

        ThrowIfFailed(result, procedureName, appConfig, correlationId);

        Log.Information("UTS delete completed (user disabled). AppId: {AppId}, CorrelationId: {CorrelationId}",
            appConfig.AppId, correlationId);
    }

    #endregion

    #region Get

    // Result columns of usp_GetUTSArchivalUser (the SP's contract); other columns are ignored
    private const string UserIdColumn = "userId";
    private const string UserNameColumn = "username";
    private const string FirstNameColumn = "firstName";
    private const string DisplayNameColumn = "displayName";
    private const string EmailColumn = "email";
    private const string FlagColumn = "Flag";
    private const string LevelsIdColumn = "LevelsID";
    private const string RoleColumn = "role";

    // UTS Users.Flag values that mean the account is not active (2 = Disable, 3 = Inactive).
    // All other flags (1 Active, 4 / 98 / 99 password states) are active accounts.
    private static readonly HashSet<int> InactiveFlags = [2, 3];

    /// <summary>
    /// Reads the user through GetSpName by LoginID. ResponseCode 2 (not found) → 404.
    /// </summary>
    public override async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig,
        string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentNullException(nameof(identifier), $"Identifier is null or empty. AppId: {appConfig.AppId}");

        var row = await FindUserRowAsync(appConfig, identifier, correlationId, cancellationToken)
                  ?? throw new HttpResponseException(HttpStatusCode.NotFound);

        return MapUser(row);
    }

    /// <summary>
    /// Calls GetSpName for the LoginID. Returns the user row, or null when the user does not exist
    /// (ResponseCode 2, or success without a row). Other failures throw via <see cref="SQLIntegration.ThrowIfFailed"/>.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, object?>?> FindUserRowAsync(AppConfig appConfig, string loginId,
        string correlationId, CancellationToken cancellationToken)
    {
        var procedureName = GetIntegrationDetails(appConfig).GetSpName;
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentException($"GetSpName is not configured for AppId {appConfig.AppId}.");

        var outputs = ResolveStatusOutputs(appConfig, HttpRequestTypes.GET);
        var inputs = new List<OdbcParameter> { CreateLookupParameter(appConfig, loginId) };

        var result =
            await ExecuteProcedureAsync(appConfig, procedureName, inputs, outputs, correlationId, cancellationToken);

        if (result.ResponseCode == (int)SqlProcedureResponseCode.NotFound)
            return null;

        ThrowIfFailed(result, procedureName, appConfig, correlationId);

        if (result.Row is null)
        {
            Log.Warning("UTS get returned success without a user row. AppId: {AppId}, CorrelationId: {CorrelationId}",
                appConfig.AppId, correlationId);
        }

        return result.Row;
    }

    private static Core2EnterpriseUser MapUser(IReadOnlyDictionary<string, object?> row)
    {
        var user = new Core2EnterpriseUser
        {
            Identifier = ReadString(row, UserIdColumn),
            UserName = ReadString(row, UserNameColumn),
            DisplayName = ReadString(row, DisplayNameColumn),
            Name = new Name { GivenName = ReadString(row, FirstNameColumn) },
            Active = ReadInt(row, FlagColumn) is { } flag && !InactiveFlags.Contains(flag)
        };

        if (ReadString(row, EmailColumn) is { } email)
        {
            user.ElectronicMailAddresses =
            [
                new ElectronicMailAddress { Value = email, ItemType = ElectronicMailAddress.Work, Primary = true }
            ];
        }

        if (ReadInt(row, LevelsIdColumn) is { } levelsId)
        {
            user.Roles =
            [
                new Role { Value = levelsId.ToString(CultureInfo.InvariantCulture), Display = ReadString(row, RoleColumn) }
            ];
        }

        return user;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string column)
    {
        return row.GetValueOrDefault(column)?.ToString()?.Trim() is { Length: > 0 } value ? value : null;
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> row, string column)
    {
        return row.GetValueOrDefault(column) is { } value
            ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
            : null;
    }

    #endregion

    /// <summary>
    /// SP parameter used to look up a user for Get / Update / Delete: the DestinationField of the POST
    /// attribute mapped from UserName (e.g. "@LoginID"). The SCIM Identifier equals LoginID (= UserName),
    /// so the same parameter serves all lookups.
    /// </summary>
    /// <exception cref="InvalidOperationException">No POST UserName mapping is configured.</exception>
    protected static string ResolveLookupParameter(AppConfig appConfig)
    {
        return GetParameterName(ResolveLookupAttribute(appConfig));
    }

    /// <summary>
    /// Input parameter carrying the user identifier (LoginID) for Get / Update / Delete, typed like the POST
    /// UserName row.
    /// </summary>
    protected static OdbcParameter CreateLookupParameter(AppConfig appConfig, string identifier)
    {
        var attribute = ResolveLookupAttribute(appConfig);

        return CreateOdbcParameter(GetParameterName(attribute), attribute.DestinationType.ToOdbcType(),
            attribute.DestinationTypeLength, identifier.Trim());
    }

    private static AttributeSchema ResolveLookupAttribute(AppConfig appConfig)
    {
        return appConfig.UserAttributeSchemas?.FirstOrDefault(a =>
                   a.HttpRequestType == HttpRequestTypes.POST
                   && !IsOutputMapping(a)
                   && string.Equals(a.SourceValue?.Trim(), "UserName", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(a.DestinationField))
               ?? throw new InvalidOperationException(
                   $"No POST 'UserName' mapping configured for AppId {appConfig.AppId}; it is required as the UTS lookup parameter.");
    }

    /// <summary>
    /// OUTPUT parameters for the operation (Get / Delete reuse the POST rows). UTS procedures always report
    /// their status, so an "Output:ResponseCode" marker is required.
    /// </summary>
    /// <exception cref="InvalidOperationException">No "Output:ResponseCode" mapping is configured.</exception>
    protected static IReadOnlyList<SqlOutputMapping> ResolveStatusOutputs(AppConfig appConfig,
        HttpRequestTypes requestType)
    {
        var outputs = GetOutputMappings(appConfig, requestType);

        if (!outputs.Any(o => o.Role == ResponseCodeRole))
        {
            throw new InvalidOperationException(
                $"No '{OutputMarker}:{ResponseCodeRole}' mapping configured for AppId {appConfig.AppId} ({requestType}).");
        }

        return outputs;
    }
}
