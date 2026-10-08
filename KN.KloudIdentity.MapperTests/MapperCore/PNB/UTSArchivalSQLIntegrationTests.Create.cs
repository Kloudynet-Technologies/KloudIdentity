using System.Data.Odbc;
using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

public partial class UTSArchivalSQLIntegrationTests
{
    private const string IntegrationDetailsJson =
        """{"AppId":"uts-archival","PostSpName":"dbo.usp_CreateUTSArchivalUser","GetSpName":"dbo.usp_GetUTSArchivalUser","PatchSpName":"dbo.usp_UpdateUTSArchivalUser","DeleteSpName":"dbo.usp_DeleteUTSArchivalUser"}""";

    /// <summary>
    /// POST rows as MgtPortal schema detection creates them for usp_CreateUTSArchivalUser (one row per SP
    /// parameter, IsRequired = true, DefaultValue = "N/A"), with the UTS mappings filled in.
    /// </summary>
    private static List<AttributeSchema> DetectedPostSchema() =>
    [
        Detected("@LoginID", AttributeDataTypes.NVarChar, "KIExtension:ExtensionAttribute1"),
        Detected("@Name", AttributeDataTypes.NVarChar, "DisplayName"),
        Detected("@LevelsID", AttributeDataTypes.Int, "Roles[0]:Value"),
        Detected("@ReferenceNo", AttributeDataTypes.NVarChar, "EnterpriseExtension:EmployeeNumber"),
        Detected("@Email", AttributeDataTypes.NVarChar, "ElectronicMailAddresses[0]:Value"),
        Detected("@Gender", AttributeDataTypes.NVarChar),
        Detected("@DOB", AttributeDataTypes.DateTime),
        Detected("@Add1", AttributeDataTypes.NVarChar),
        Detected("@Add2", AttributeDataTypes.NVarChar),
        Detected("@Add3", AttributeDataTypes.NVarChar),
        Detected("@Postcode", AttributeDataTypes.NVarChar),
        Detected("@Town", AttributeDataTypes.NVarChar),
        Detected("@State", AttributeDataTypes.NVarChar),
        Detected("@Contact", AttributeDataTypes.NVarChar),
        Detected("@CreatedBy", AttributeDataTypes.BigInt, "0", MappingTypes.Constant),
        Detected("@ResponseCode", AttributeDataTypes.Int, "Output:ResponseCode", MappingTypes.Constant),
        Detected("@ResponseMessage", AttributeDataTypes.NVarChar, "Output:ResponseMessage", MappingTypes.Constant)
    ];

    private static AttributeSchema Detected(string parameter, AttributeDataTypes type, string sourceValue = "",
        MappingTypes mappingType = MappingTypes.Direct) => new()
    {
        HttpRequestType = HttpRequestTypes.POST,
        DestinationField = parameter,
        DestinationType = type,
        DestinationTypeLength = type == AttributeDataTypes.NVarChar ? 200 : null,
        SourceValue = sourceValue,
        MappingType = mappingType,
        IsRequired = true,
        DefaultValue = "N/A"
    };

    private static Core2EnterpriseUser EntraUser(string? role = "1", string? email = "ali@pnb.com",
        string displayName = "Ali Bin Abu")
    {
        var user = new Core2EnterpriseUser
        {
            UserName = " ASNB9999 ",
            DisplayName = displayName,
            Roles = role is null ? [] : [new Role { Value = role }],
            ElectronicMailAddresses = email is null ? [] : [new ElectronicMailAddress { Value = email }],
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2 { EmployeeNumber = "S12345" }
        };
        // The UTS lookup (LoginID) is mapped from KIExtension:ExtensionAttribute1
        user.KIExtension.ExtensionAttribute1 = " ASNB9999 ";
        return user;
    }

    private static AppConfig CreateCreateConfig(List<AttributeSchema>? schema = null)
    {
        var config = CreateAppConfig((schema ?? DetectedPostSchema()).ToArray());
        config.IntegrationDetails = IntegrationDetailsJson;
        return config;
    }

    private async Task<List<OdbcParameter>> MapAsync(Core2EnterpriseUser user, List<AttributeSchema>? schema = null)
    {
        var config = CreateCreateConfig(schema);
        var payload = await _sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), user, config);
        return Assert.IsAssignableFrom<List<OdbcParameter>>(payload);
    }

    private static object ValueOf(List<OdbcParameter> parameters, string name) =>
        Assert.Single(parameters, p => p.ParameterName == name).Value;

    #region Mapping

    [Fact]
    public async Task MapAndPreparePayloadAsync_DetectedSchema_EmitsOnlyMappedInputs()
    {
        var parameters = await MapAsync(EntraUser());

        Assert.Equal(["@LoginID", "@Name", "@LevelsID", "@ReferenceNo", "@Email", "@CreatedBy"],
            parameters.Select(p => p.ParameterName));
        Assert.Equal("ASNB9999", ValueOf(parameters, "@LoginID"));
        Assert.Equal("Ali Bin Abu", ValueOf(parameters, "@Name"));
        Assert.Equal(1, ValueOf(parameters, "@LevelsID"));
        Assert.Equal(OdbcType.Int, Assert.Single(parameters, p => p.ParameterName == "@LevelsID").OdbcType);
        Assert.Equal("S12345", ValueOf(parameters, "@ReferenceNo"));
        Assert.Equal("ali@pnb.com", ValueOf(parameters, "@Email"));
        Assert.Equal(0L, ValueOf(parameters, "@CreatedBy"));
        Assert.DoesNotContain(parameters, p => Equals(p.Value, "N/A"));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_MissingMappedValues_AreSentAsDbNull()
    {
        var parameters = await MapAsync(EntraUser(role: null, email: null, displayName: "   "));

        // Mapped parameters are always emitted, so the SP validates them (code 1) instead of SQL error 201
        Assert.Equal(DBNull.Value, ValueOf(parameters, "@Name"));
        Assert.Equal(DBNull.Value, ValueOf(parameters, "@LevelsID"));
        Assert.Equal(DBNull.Value, ValueOf(parameters, "@Email"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task MapAndPreparePayloadAsync_LevelsIdZeroOrUnparsable_IsSentAsDbNull(string role)
    {
        var parameters = await MapAsync(EntraUser(role: role));

        Assert.Equal(DBNull.Value, ValueOf(parameters, "@LevelsID"));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_UsableDefaultValue_IsAppliedWhenValueMissing()
    {
        var schema = DetectedPostSchema();
        var email = schema.FindIndex(a => a.DestinationField == "@Email");
        schema[email] = schema[email] with { DefaultValue = "noreply@pnb.com" };

        var parameters = await MapAsync(EntraUser(email: null), schema);

        Assert.Equal("noreply@pnb.com", ValueOf(parameters, "@Email"));
    }

    [Theory]
    [InlineData("0001-01-01", null)]
    [InlineData("1990-05-01", "1990-05-01")]
    public async Task MapAndPreparePayloadAsync_DateTime_MinValueIsSentAsDbNull(string constant, string? expected)
    {
        var schema = DetectedPostSchema();
        var dob = schema.FindIndex(a => a.DestinationField == "@DOB");
        schema[dob] = schema[dob] with { SourceValue = constant, MappingType = MappingTypes.Constant };

        var parameters = await MapAsync(EntraUser(), schema);

        Assert.Equal(expected is null ? DBNull.Value : DateTime.Parse(expected), ValueOf(parameters, "@DOB"));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_InvalidConstant_ThrowsArgumentException()
    {
        var schema = DetectedPostSchema();
        var createdBy = schema.FindIndex(a => a.DestinationField == "@CreatedBy");
        schema[createdBy] = schema[createdBy] with { SourceValue = "system" };

        await Assert.ThrowsAsync<ArgumentException>(() => MapAsync(EntraUser(), schema));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_NoMappedRows_ThrowsInvalidOperationException()
    {
        var schema = DetectedPostSchema()
            .Select(a => SQLIntegrationOutputOrEmpty(a))
            .ToList();

        await Assert.ThrowsAsync<InvalidOperationException>(() => MapAsync(EntraUser(), schema));
    }

    [Fact]
    public async Task ProvisionAsync_NoPostRows_ThrowsBeforeCallingProcedure()
    {
        // An empty schema maps to an empty payload (ReplaceUserV4 passes no rows for UTS); Create rejects it
        var sut = CreateCreateSut(Status(0));
        var config = CreateCreateConfig();
        var payload = await sut.MapAndPreparePayloadAsync([], EntraUser(), config);

        await Assert.ThrowsAsync<ArgumentNullException>(() => (Task)sut.ProvisionAsync(payload, config, "correlation-id"));
        Assert.Empty(sut.Calls);
    }

    private static AttributeSchema SQLIntegrationOutputOrEmpty(AttributeSchema a) =>
        a.SourceValue.StartsWith("Output", StringComparison.Ordinal) ? a : a with { SourceValue = "" };

    #endregion

    #region Create (ProvisionAsync)

    private static TestableUTSArchivalSQLIntegration CreateSut(SqlProcedureResult result) =>
        new(Options.Create(new AppSettings())) { Result = result };

    private const string GetSp = "dbo.usp_GetUTSArchivalUser";
    private const string CreateSp = "dbo.usp_CreateUTSArchivalUser";
    private const string UpdateSp = "dbo.usp_UpdateUTSArchivalUser";

    /// <summary>
    /// SUT for Create tests: the pre-create lookup (GetSpName) reports "not found" unless overridden.
    /// </summary>
    private static TestableUTSArchivalSQLIntegration CreateCreateSut(SqlProcedureResult createResult)
    {
        var sut = CreateSut(createResult);
        sut.ResultsByProcedure[GetSp] = Status(2);
        return sut;
    }

    private static SqlProcedureResult Status(int code, IReadOnlyDictionary<string, object?>? row = null) =>
        new(code, code == 0 ? "Success" : "Failure", row, new Dictionary<string, object?>());

    [Fact]
    public async Task ProvisionAsync_Success_ReturnsLoginIdAsIdentifier()
    {
        var sut = CreateCreateSut(Status(0, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["LoginID"] = "ASNB9999", ["Flag"] = 99 }));
        var config = CreateCreateConfig();
        var payload = await sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), EntraUser(), config);

        Core2EnterpriseUser? result = await sut.ProvisionAsync(payload, config, "correlation-id");

        Assert.NotNull(result);
        Assert.Equal("ASNB9999", result!.Identifier);
        Assert.Equal("ASNB9999", result.UserName);

        Assert.Equal([GetSp, CreateSp], sut.Calls.Select(c => c.ProcedureName));
        var call = sut.Calls[^1];
        Assert.Equal(6, call.Inputs.Count);
        Assert.Equal(["@ResponseCode", "@ResponseMessage"], call.Outputs.Select(o => o.ParameterName));
    }

    [Fact]
    public async Task ProvisionAsync_SuccessWithoutRow_FallsBackToSentLoginId()
    {
        var sut = CreateCreateSut(Status(0));
        var config = CreateCreateConfig();
        var payload = await sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), EntraUser(), config);

        Core2EnterpriseUser? result = await sut.ProvisionAsync(payload, config, "correlation-id");

        Assert.Equal("ASNB9999", result!.Identifier);
    }

    [Fact]
    public async Task ProvisionAsync_V2Overload_UsesUtsCreate()
    {
        var sut = CreateCreateSut(Status(0));
        var config = CreateCreateConfig();
        var payload = await sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), EntraUser(), config);

        Core2EnterpriseUser? result = await sut.ProvisionAsync(payload, "uts-archival", config, null!, "correlation-id");

        Assert.Equal("ASNB9999", result!.Identifier);
    }

    [Theory]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(3, HttpStatusCode.Conflict)]
    [InlineData(4, HttpStatusCode.InternalServerError)]
    public async Task ProvisionAsync_Failure_ThrowsMappedHttpStatus(int responseCode, HttpStatusCode expected)
    {
        var sut = CreateCreateSut(Status(responseCode));
        var config = CreateCreateConfig();
        var payload = await sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), EntraUser(), config);

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            (Task)sut.ProvisionAsync(payload, config, "correlation-id"));

        Assert.Equal(expected, exception.Response.StatusCode);
    }

    [Fact]
    public async Task ProvisionAsync_NoResponseCodeMarker_ThrowsBeforeCallingProcedure()
    {
        var schema = DetectedPostSchema().Where(a => a.DestinationField != "@ResponseCode").ToList();
        var sut = CreateCreateSut(Status(0));
        var config = CreateCreateConfig(schema);
        var payload = await sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), EntraUser(), config);

        await Assert.ThrowsAsync<InvalidOperationException>(() => (Task)sut.ProvisionAsync(payload, config, "correlation-id"));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task ProvisionAsync_NoUserNameMapping_ThrowsBeforeCallingProcedure()
    {
        var schema = DetectedPostSchema();
        var login = schema.FindIndex(a => a.DestinationField == "@LoginID");
        schema[login] = schema[login] with { SourceValue = "EnterpriseExtension:EmployeeNumber" };
        var sut = CreateCreateSut(Status(0));
        var config = CreateCreateConfig(schema);

        // Config error surfaces during mapping (Update inputs need the lookup parameter) or provisioning
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var payload = await sut.MapAndPreparePayloadAsync(config.UserAttributeSchemas.ToList(), EntraUser(), config);
            await (Task)sut.ProvisionAsync(payload, config, "correlation-id");
        });
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task ProvisionAsync_InvalidPayload_ThrowsArgumentNullException()
    {
        var sut = CreateCreateSut(Status(0));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            (Task)sut.ProvisionAsync(new { Invalid = true }, CreateCreateConfig(), "correlation-id"));
        Assert.Empty(sut.Calls);
    }

    #endregion
}
