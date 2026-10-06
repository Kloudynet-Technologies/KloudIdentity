using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

/// <summary>
/// Create when the LoginID already exists in UTS: the user is updated through PatchSpName instead of created.
/// </summary>
public partial class UTSArchivalSQLIntegrationTests
{
    /// <summary>
    /// PATCH rows as schema detection creates them for usp_UpdateUTSArchivalUser, with the UTS mappings filled in.
    /// </summary>
    private static List<AttributeSchema> DetectedPatchSchema() =>
        new List<AttributeSchema>
        {
            Detected("@LoginID", AttributeDataTypes.NVarChar),
            Detected("@Name", AttributeDataTypes.NVarChar, "DisplayName"),
            Detected("@LevelsID", AttributeDataTypes.Int, "Roles[0]:Value"),
            Detected("@Flag", AttributeDataTypes.Int),
            Detected("@Email", AttributeDataTypes.NVarChar, "ElectronicMailAddresses[0]:Value"),
            Detected("@Gender", AttributeDataTypes.NVarChar),
            Detected("@DOB", AttributeDataTypes.DateTime),
            Detected("@Contact", AttributeDataTypes.NVarChar),
            Detected("@UpdatedBy", AttributeDataTypes.BigInt, "0", MappingTypes.Constant),
            Detected("@ResponseCode", AttributeDataTypes.Int, "Output:ResponseCode", MappingTypes.Constant),
            Detected("@ResponseMessage", AttributeDataTypes.NVarChar, "Output:ResponseMessage", MappingTypes.Constant)
        }.Select(a => a with { HttpRequestType = HttpRequestTypes.PATCH }).ToList();

    private static AppConfig CreateUpsertConfig(List<AttributeSchema>? patchSchema = null) =>
        CreateCreateConfig(DetectedPostSchema().Concat(patchSchema ?? DetectedPatchSchema()).ToList());

    /// <summary>
    /// Maps the POST rows (as CreateUserV4 does) and runs ProvisionAsync.
    /// </summary>
    private static async Task<Core2EnterpriseUser?> CreateAsync(TestableUTSArchivalSQLIntegration sut, AppConfig config,
        Core2EnterpriseUser? user = null)
    {
        var postRows = config.UserAttributeSchemas.Where(a => a.HttpRequestType == HttpRequestTypes.POST).ToList();
        var payload = await sut.MapAndPreparePayloadAsync(postRows, user ?? EntraUser(), config);
        return await sut.ProvisionAsync(payload, config, "correlation-id");
    }

    private static TestableUTSArchivalSQLIntegration CreateSutForExistingUser(int updateCode = 0, int? existingFlag = 2)
    {
        var sut = CreateSut(Status(0));
        sut.ResultsByProcedure[GetSp] = Status(0, GetRow(flag: existingFlag) is var row
            ? new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase) { ["userId"] = "ASNB9999", ["username"] = "ASNB9999" }
            : null);
        sut.ResultsByProcedure[UpdateSp] = Status(updateCode);
        return sut;
    }

    [Fact]
    public async Task ProvisionAsync_ExistingUser_UpdatesInsteadOfCreating()
    {
        var sut = CreateSutForExistingUser();

        var result = await CreateAsync(sut, CreateUpsertConfig());

        Assert.Equal("ASNB9999", result!.Identifier);
        Assert.Equal("ASNB9999", result.UserName);
        Assert.Equal([GetSp, UpdateSp], sut.Calls.Select(c => c.ProcedureName));

        var lookup = Assert.Single(sut.Calls[0].Inputs);
        Assert.Equal("@LoginID", lookup.ParameterName);
        Assert.Equal("ASNB9999", lookup.Value);
    }

    [Fact]
    public async Task ProvisionAsync_ExistingUser_SendsPatchMappingAndExistingFlag()
    {
        var sut = CreateSutForExistingUser(existingFlag: 2);

        await CreateAsync(sut, CreateUpsertConfig());

        var update = sut.Calls[^1];
        Assert.Equal(["@LoginID", "@Name", "@LevelsID", "@Email", "@UpdatedBy", "@Flag"], update.Inputs.Select(p => p.ParameterName));
        Assert.Equal("ASNB9999", update.Inputs[0].Value);
        Assert.Equal("Ali Bin Abu", update.Inputs[1].Value);
        Assert.Equal(1, update.Inputs[2].Value);
        Assert.Equal("ali@pnb.com", update.Inputs[3].Value);
        Assert.Equal(0L, update.Inputs[4].Value);

        // Status of the existing account is kept (Flag from GetSpName, even though Entra sends active = true)
        Assert.Equal(2, update.Inputs[5].Value);

        // POST-only parameters are not sent to the Update SP
        Assert.DoesNotContain(update.Inputs, p => p.ParameterName is "@ReferenceNo" or "@CreatedBy");
        Assert.Equal(["@ResponseCode", "@ResponseMessage"], update.Outputs.Select(o => o.ParameterName));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, null)]
    [InlineData(98, null)]
    [InlineData(99, null)]
    [InlineData(null, null)]
    public async Task ProvisionAsync_ExistingUser_SendsExistingFlagOnlyWhenUpdateSpAcceptsIt(int? existingFlag, int? expectedFlag)
    {
        var sut = CreateSutForExistingUser(existingFlag: existingFlag);

        await CreateAsync(sut, CreateUpsertConfig());

        // usp_UpdateUTSArchivalUser accepts @Flag 1 / 2 / 3 only; omitted @Flag also keeps the current Flag
        var flag = sut.Calls[^1].Inputs.SingleOrDefault(p => p.ParameterName == "@Flag");
        Assert.Equal(expectedFlag, flag?.Value as int?);
    }

    [Fact]
    public async Task ProvisionAsync_ExistingUser_IgnoresFlagAndLoginIdMappedOnPatch()
    {
        var patch = DetectedPatchSchema();
        patch[patch.FindIndex(a => a.DestinationField == "@Flag")] = patch.First(a => a.DestinationField == "@Flag") with { SourceValue = "1", MappingType = MappingTypes.Constant };
        patch[patch.FindIndex(a => a.DestinationField == "@LoginID")] = patch.First(a => a.DestinationField == "@LoginID") with { SourceValue = "DisplayName" };
        var sut = CreateSutForExistingUser();

        await CreateAsync(sut, CreateUpsertConfig(patch));

        var update = sut.Calls[^1];
        Assert.Equal("ASNB9999", Assert.Single(update.Inputs, p => p.ParameterName == "@LoginID").Value);

        // The mapped Constant 1 is ignored; the existing Flag (2) is sent
        Assert.Equal(2, Assert.Single(update.Inputs, p => p.ParameterName == "@Flag").Value);
    }

    [Fact]
    public async Task ProvisionAsync_ExistingUser_MissingValuesAreSentAsDbNull()
    {
        var sut = CreateSutForExistingUser();

        await CreateAsync(sut, CreateUpsertConfig(), EntraUser(email: null));

        // NULL means "unchanged" for usp_UpdateUTSArchivalUser
        Assert.Equal(DBNull.Value, sut.Calls[^1].Inputs.Single(p => p.ParameterName == "@Email").Value);
    }

    [Fact]
    public async Task ProvisionAsync_ExistingUserWithoutPatchMapping_LinksWithoutUpdate()
    {
        var sut = CreateSutForExistingUser();

        var result = await CreateAsync(sut, CreateCreateConfig());

        Assert.Equal("ASNB9999", result!.Identifier);
        Assert.Equal([GetSp], sut.Calls.Select(c => c.ProcedureName));
    }

    [Theory]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(4, HttpStatusCode.InternalServerError)]
    public async Task ProvisionAsync_ExistingUserUpdateFails_ThrowsMappedHttpStatus(int updateCode, HttpStatusCode expected)
    {
        var sut = CreateSutForExistingUser(updateCode);

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() => CreateAsync(sut, CreateUpsertConfig()));

        Assert.Equal(expected, exception.Response.StatusCode);
        Assert.DoesNotContain(sut.Calls, c => c.ProcedureName == CreateSp);
    }

    [Fact]
    public async Task ProvisionAsync_LookupSystemError_ThrowsWithoutCreating()
    {
        var sut = CreateSut(Status(0));
        sut.ResultsByProcedure[GetSp] = Status(4);

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() => CreateAsync(sut, CreateUpsertConfig()));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.Response.StatusCode);
        Assert.Equal([GetSp], sut.Calls.Select(c => c.ProcedureName));
    }

    [Fact]
    public async Task ProvisionAsync_NewUser_LooksUpThenCreates()
    {
        var sut = CreateCreateSut(Status(0));

        var result = await CreateAsync(sut, CreateUpsertConfig());

        Assert.Equal("ASNB9999", result!.Identifier);
        Assert.Equal([GetSp, CreateSp], sut.Calls.Select(c => c.ProcedureName));
    }

    [Fact]
    public async Task ProvisionAsync_NoLoginIdValue_SkipsLookupAndLetsCreateValidate()
    {
        var sut = CreateCreateSut(Status(1));
        var user = EntraUser();
        user.UserName = null!;

        await Assert.ThrowsAsync<HttpResponseException>(() => CreateAsync(sut, CreateUpsertConfig(), user));

        Assert.Equal([CreateSp], sut.Calls.Select(c => c.ProcedureName));
    }

    [Fact]
    public async Task ProvisionAsync_ExistingUserWithPatchMappingButNoPatchSp_ThrowsArgumentException()
    {
        var sut = CreateSutForExistingUser();
        var config = CreateUpsertConfig();
        config.IntegrationDetails = """{"AppId":"uts-archival","PostSpName":"dbo.usp_CreateUTSArchivalUser","GetSpName":"dbo.usp_GetUTSArchivalUser"}""";

        await Assert.ThrowsAsync<ArgumentException>(() => CreateAsync(sut, config));
        Assert.Equal([GetSp], sut.Calls.Select(c => c.ProcedureName));
    }
}
