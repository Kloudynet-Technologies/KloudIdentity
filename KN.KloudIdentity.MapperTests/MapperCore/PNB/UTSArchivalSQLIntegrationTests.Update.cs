using System.Data.Odbc;
using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

/// <summary>
/// Update (PATCH): only patched attributes are sent; @Flag follows an explicitly patched "active".
/// </summary>
public partial class UTSArchivalSQLIntegrationTests
{
    private static (TestableUTSArchivalSQLIntegration Sut, PatchOperationContext Context) CreateUpdateSut(
        int updateCode = 0, int? currentFlag = 1)
    {
        var context = new PatchOperationContext();
        var sut = new TestableUTSArchivalSQLIntegration(Options.Create(new AppSettings()), context)
        {
            Result = Status(0)
        };
        sut.ResultsByProcedure[UpdateSp] = Status(updateCode);
        sut.ResultsByProcedure[GetSp] = Status(0, GetRow(flag: currentFlag));
        return (sut, context);
    }

    private static void Patch(PatchOperationContext context, params (OperationName Name, string Path)[] operations) =>
        context.Capture(operations.Select(o => (PatchOperation2Base)new PatchOperation2Combined(o.Name, o.Path)));

    /// <summary>
    /// The user as UpdateUserV4 builds it: an empty user with only the patched values applied.
    /// </summary>
    private static Core2EnterpriseUser PatchedUser(string? displayName = null, bool active = false, string? email = null) => new()
    {
        Identifier = "ASNB9999",
        DisplayName = displayName!,
        Active = active,
        ElectronicMailAddresses = email is null ? [] : [new ElectronicMailAddress { Value = email }]
    };

    private static AppConfig CreateUpdateConfig(List<AttributeSchema>? patchSchema = null) => CreateUpsertConfig(patchSchema);

    /// <summary>
    /// Maps the PATCH rows (as UpdateUserV4 does) and runs UpdateAsync.
    /// </summary>
    private static async Task UpdateAsync(TestableUTSArchivalSQLIntegration sut, AppConfig config, Core2EnterpriseUser user)
    {
        var patchRows = config.UserAttributeSchemas.Where(a => a.HttpRequestType == HttpRequestTypes.PATCH).ToList();
        var payload = await sut.MapAndPreparePayloadAsync(patchRows, user, config);
        await sut.UpdateAsync(payload, user, config, "correlation-id");
    }

    private static object ValueOf((string ProcedureName, IReadOnlyList<OdbcParameter> Inputs, IReadOnlyList<SqlOutputMapping> Outputs) call, string name) =>
        Assert.Single(call.Inputs, p => p.ParameterName == name).Value;

    #region Attributes

    [Fact]
    public async Task UpdateAsync_OnlyPatchedAttributesAreSent()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "displayName"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(displayName: "Ali Bin Abu"));

        var call = Assert.Single(sut.Calls);
        Assert.Equal(UpdateSp, call.ProcedureName);
        Assert.Equal(["@LoginID", "@Name", "@LevelsID", "@Email", "@UpdatedBy"], call.Inputs.Select(p => p.ParameterName));
        Assert.Equal("ASNB9999", ValueOf(call, "@LoginID"));
        Assert.Equal("Ali Bin Abu", ValueOf(call, "@Name"));
        Assert.Equal(DBNull.Value, ValueOf(call, "@LevelsID")); // not patched → unchanged
        Assert.Equal(DBNull.Value, ValueOf(call, "@Email"));    // not patched → unchanged
        Assert.Equal(0L, ValueOf(call, "@UpdatedBy"));          // Constant → always sent
        Assert.Equal(["@ResponseCode", "@ResponseMessage"], call.Outputs.Select(o => o.ParameterName));
    }

    [Fact]
    public async Task UpdateAsync_RemovedStringAttribute_IsClearedWithEmptyString()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Remove, "emails[type eq \"work\"].value"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser());

        Assert.Equal(string.Empty, ValueOf(sut.Calls[^1], "@Email"));
    }

    [Fact]
    public async Task UpdateAsync_PatchedToEmptyName_IsSentAsEmptyString()
    {
        // The SP rejects an empty @Name (ResponseCode 1 → 400); the connector does not hide it
        var (sut, context) = CreateUpdateSut(updateCode: 1);
        Patch(context, (OperationName.Replace, "displayName"));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(displayName: "")));

        Assert.Equal(string.Empty, ValueOf(sut.Calls[^1], "@Name"));
        Assert.Equal(HttpStatusCode.BadRequest, exception.Response.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_RemovedNonStringAttribute_IsLeftUnchanged()
    {
        var patch = DetectedPatchSchema();
        var dob = patch.FindIndex(a => a.DestinationField == "@DOB");
        patch[dob] = patch[dob] with { SourceValue = "KIExtension:ExtensionAttribute3" };
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Remove, SchemaIdentifiers.Core2KIUser + ":extensionAttribute3"),
            (OperationName.Replace, "displayName"));

        await UpdateAsync(sut, CreateUpdateConfig(patch), PatchedUser(displayName: "Ali"));

        // UTS SPs treat NULL as "unchanged"; a DATETIME cannot be cleared
        Assert.Equal(DBNull.Value, ValueOf(sut.Calls[^1], "@DOB"));
    }

    [Fact]
    public async Task UpdateAsync_PatchedValuesAreConverted()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "roles[primary eq \"True\"].value"),
            (OperationName.Add, "emails[type eq \"work\"].value"));
        var user = PatchedUser(email: " new@pnb.com ");
        user.Roles = [new Role { Value = "3" }];

        await UpdateAsync(sut, CreateUpdateConfig(), user);

        Assert.Equal(3, ValueOf(sut.Calls[^1], "@LevelsID"));
        Assert.Equal("new@pnb.com", ValueOf(sut.Calls[^1], "@Email"));
        Assert.Equal(DBNull.Value, ValueOf(sut.Calls[^1], "@Name"));
    }

    [Fact]
    public async Task UpdateAsync_NothingMappedIsPatched_DoesNotCallProcedure()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "title"), (OperationName.Replace, "preferredLanguage"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser());

        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task UpdateAsync_FlagAndLoginIdMappedOnPatch_AreIgnored()
    {
        var patch = DetectedPatchSchema();
        patch[patch.FindIndex(a => a.DestinationField == "@Flag")] = patch.First(a => a.DestinationField == "@Flag") with { SourceValue = "Active" };
        patch[patch.FindIndex(a => a.DestinationField == "@LoginID")] = patch.First(a => a.DestinationField == "@LoginID") with { SourceValue = "KIExtension:ExtensionAttribute1" };
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "displayName"), (OperationName.Replace, "userName"));

        await UpdateAsync(sut, CreateUpdateConfig(patch), PatchedUser(displayName: "Ali"));

        var call = sut.Calls[^1];
        Assert.Equal("ASNB9999", Assert.Single(call.Inputs, p => p.ParameterName == "@LoginID").Value); // SCIM Identifier
        Assert.DoesNotContain(call.Inputs, p => p.ParameterName == "@Flag"); // active not patched
    }

    [Fact]
    public async Task UpdateAsync_InvalidSourceValue_FailsLoudly()
    {
        var patch = DetectedPatchSchema();
        var levels = patch.FindIndex(a => a.DestinationField == "@LevelsID");
        patch[levels] = patch[levels] with { SourceValue = "Role[0]:Value" }; // typo of Roles
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "displayName"));

        await Assert.ThrowsAsync<ArgumentException>(() => UpdateAsync(sut, CreateUpdateConfig(patch), PatchedUser(displayName: "Ali")));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task UpdateAsync_WithoutCapturedPatch_SendsValuesAsMapped()
    {
        var (sut, _) = CreateUpdateSut();

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(displayName: "Ali", email: null));

        var call = Assert.Single(sut.Calls);
        Assert.Equal("Ali", ValueOf(call, "@Name"));
        Assert.Equal(DBNull.Value, ValueOf(call, "@Email")); // missing → unchanged, never cleared
        Assert.DoesNotContain(call.Inputs, p => p.ParameterName == "@Flag");
    }

    #endregion

    #region Status (@Flag)

    [Fact]
    public async Task UpdateAsync_ActiveFalse_SendsFlag2WithoutLookup()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "active"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(active: false));

        var call = Assert.Single(sut.Calls);
        Assert.Equal(UpdateSp, call.ProcedureName);
        Assert.Equal(2, ValueOf(call, "@Flag"));
        Assert.All(call.Inputs.Where(p => p.ParameterName is "@Name" or "@LevelsID" or "@Email"),
            p => Assert.Equal(DBNull.Value, p.Value));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task UpdateAsync_ActiveTrueOnInactiveAccount_SendsFlag1(int currentFlag)
    {
        var (sut, context) = CreateUpdateSut(currentFlag: currentFlag);
        Patch(context, (OperationName.Replace, "active"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(active: true));

        Assert.Equal([GetSp, UpdateSp], sut.Calls.Select(c => c.ProcedureName));
        Assert.Equal(1, ValueOf(sut.Calls[^1], "@Flag"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(98)]
    [InlineData(99)]
    public async Task UpdateAsync_ActiveTrueOnActiveAccount_DoesNotSendFlag(int currentFlag)
    {
        // @Flag 1 on Flag 99 / 4 / 98 would skip the forced password change (known SP defect)
        var (sut, context) = CreateUpdateSut(currentFlag: currentFlag);
        Patch(context, (OperationName.Replace, "active"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(active: true));

        Assert.Equal([GetSp], sut.Calls.Select(c => c.ProcedureName)); // nothing else changed → no Update
    }

    [Fact]
    public async Task UpdateAsync_ActiveTrueOnActiveAccountWithOtherChanges_UpdatesWithoutFlag()
    {
        var (sut, context) = CreateUpdateSut(currentFlag: 99);
        Patch(context, (OperationName.Replace, "active"), (OperationName.Replace, "displayName"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(displayName: "Ali", active: true));

        Assert.Equal([GetSp, UpdateSp], sut.Calls.Select(c => c.ProcedureName));
        Assert.DoesNotContain(sut.Calls[^1].Inputs, p => p.ParameterName == "@Flag");
    }

    [Fact]
    public async Task UpdateAsync_ActiveRemoved_DoesNotChangeStatus()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Remove, "active"), (OperationName.Replace, "displayName"));

        await UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(displayName: "Ali"));

        Assert.Equal([UpdateSp], sut.Calls.Select(c => c.ProcedureName));
        Assert.DoesNotContain(sut.Calls[^1].Inputs, p => p.ParameterName == "@Flag");
    }

    [Fact]
    public async Task UpdateAsync_ActiveTrueForUnknownUser_Throws404()
    {
        var (sut, context) = CreateUpdateSut();
        sut.ResultsByProcedure[GetSp] = Status(2);
        Patch(context, (OperationName.Replace, "active"));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(active: true)));

        Assert.Equal(HttpStatusCode.NotFound, exception.Response.StatusCode);
    }

    #endregion

    #region Errors

    [Theory]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(2, HttpStatusCode.NotFound)]
    [InlineData(4, HttpStatusCode.InternalServerError)]
    public async Task UpdateAsync_Failure_ThrowsMappedHttpStatus(int updateCode, HttpStatusCode expected)
    {
        var (sut, context) = CreateUpdateSut(updateCode);
        Patch(context, (OperationName.Replace, "displayName"));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            UpdateAsync(sut, CreateUpdateConfig(), PatchedUser(displayName: "Ali")));

        Assert.Equal(expected, exception.Response.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_NoPatchSpName_ThrowsArgumentException()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "displayName"));
        var config = CreateUpdateConfig();
        config.IntegrationDetails = """{"AppId":"uts-archival","PostSpName":"dbo.usp_CreateUTSArchivalUser","GetSpName":"dbo.usp_GetUTSArchivalUser"}""";

        await Assert.ThrowsAsync<ArgumentException>(() => UpdateAsync(sut, config, PatchedUser(displayName: "Ali")));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task UpdateAsync_MissingIdentifier_ThrowsBeforeCallingProcedure()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "displayName"));
        var user = PatchedUser(displayName: "Ali");
        user.Identifier = " ";

        await Assert.ThrowsAsync<ArgumentNullException>(() => UpdateAsync(sut, CreateUpdateConfig(), user));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task UpdateAsync_V2Overload_UsesUtsUpdate()
    {
        var (sut, context) = CreateUpdateSut();
        Patch(context, (OperationName.Replace, "displayName"));
        var config = CreateUpdateConfig();
        var user = PatchedUser(displayName: "Ali");
        var payload = await sut.MapAndPreparePayloadAsync(
            config.UserAttributeSchemas.Where(a => a.HttpRequestType == HttpRequestTypes.PATCH).ToList(), user, config);

        await sut.UpdateAsync(payload, user, "uts-archival", config, null!, "correlation-id");

        Assert.Equal([UpdateSp], sut.Calls.Select(c => c.ProcedureName));
    }

    #endregion
}
