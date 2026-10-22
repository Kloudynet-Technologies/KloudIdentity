using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

/// <summary>
/// Replace (PUT): full update through PatchSpName with the PATCH rows; status follows "active" in the body.
/// </summary>
public partial class UTSArchivalSQLIntegrationTests
{
    private static Core2EnterpriseUser PutUser(bool active = true) => new()
    {
        Identifier = "ASNB9999",
        UserName = "ASNB9999",
        DisplayName = "Bob Is Amazing",
        Active = active,
        Roles = [new Role { Value = "3" }],
        ElectronicMailAddresses = [new ElectronicMailAddress { Value = "bob@pnb.com" }]
    };

    /// <summary>
    /// As ReplaceUserV4 does: maps the PUT rows (none for UTS), then calls ReplaceAsync.
    /// </summary>
    private static async Task ReplaceAsync(TestableUTSArchivalSQLIntegration sut, AppConfig config, Core2EnterpriseUser user)
    {
        var putRows = config.UserAttributeSchemas.Where(a => a.HttpRequestType == HttpRequestTypes.PUT).ToList();
        var payload = await sut.MapAndPreparePayloadAsync(putRows, user, config);
        await sut.ReplaceAsync(payload, user, config, "correlation-id");
    }

    [Fact]
    public async Task ReplaceAsync_WithoutPutRows_UpdatesAllPatchMappedAttributes()
    {
        var (sut, _) = CreateUpdateSut(currentFlag: 1);

        await ReplaceAsync(sut, CreateUpdateConfig(), PutUser(active: true));

        var call = sut.Calls[^1];
        Assert.Equal(UpdateSp, call.ProcedureName);
        Assert.Equal("ASNB9999", ValueOf(call, "@LoginID"));
        Assert.Equal("Bob Is Amazing", ValueOf(call, "@Name"));
        Assert.Equal(3, ValueOf(call, "@LevelsID"));
        Assert.Equal("bob@pnb.com", ValueOf(call, "@Email"));
        Assert.Equal(0L, ValueOf(call, "@UpdatedBy"));
        Assert.DoesNotContain(call.Inputs, p => p.ParameterName == "@Flag"); // already active
    }

    [Fact]
    public async Task ReplaceAsync_ActiveFalse_SendsFlag2()
    {
        var (sut, _) = CreateUpdateSut();

        await ReplaceAsync(sut, CreateUpdateConfig(), PutUser(active: false));

        Assert.Equal([UpdateSp], sut.Calls.Select(c => c.ProcedureName));
        Assert.Equal(2, ValueOf(sut.Calls[^1], "@Flag"));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(99, null)]
    [InlineData(4, null)]
    public async Task ReplaceAsync_ActiveTrue_SendsFlag1OnlyForInactiveAccount(int currentFlag, int? expectedFlag)
    {
        var (sut, _) = CreateUpdateSut(currentFlag: currentFlag);

        await ReplaceAsync(sut, CreateUpdateConfig(), PutUser(active: true));

        Assert.Equal([GetSp, UpdateSp], sut.Calls.Select(c => c.ProcedureName));
        var flag = sut.Calls[^1].Inputs.SingleOrDefault(p => p.ParameterName == "@Flag");
        Assert.Equal(expectedFlag, flag?.Value as int?);
    }

    [Fact]
    public async Task ReplaceAsync_MissingValues_AreLeftUnchanged()
    {
        var (sut, _) = CreateUpdateSut();
        var user = PutUser(active: false);
        user.ElectronicMailAddresses = [];

        await ReplaceAsync(sut, CreateUpdateConfig(), user);

        Assert.Equal(DBNull.Value, ValueOf(sut.Calls[^1], "@Email"));
    }

    [Theory]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(2, HttpStatusCode.NotFound)]
    [InlineData(4, HttpStatusCode.InternalServerError)]
    public async Task ReplaceAsync_Failure_ThrowsMappedHttpStatus(int updateCode, HttpStatusCode expected)
    {
        var (sut, _) = CreateUpdateSut(updateCode);

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            ReplaceAsync(sut, CreateUpdateConfig(), PutUser(active: false)));

        Assert.Equal(expected, exception.Response.StatusCode);
    }

    [Fact]
    public async Task ReplaceAsync_NoPatchMapping_ThrowsInvalidOperationException()
    {
        var (sut, _) = CreateUpdateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReplaceAsync(sut, CreateCreateConfig(), PutUser()));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task ReplaceAsync_V2Overload_UsesUtsReplace()
    {
        var (sut, _) = CreateUpdateSut();
        var config = CreateUpdateConfig();
        var user = PutUser(active: false);

        var result = await sut.ReplaceAsync(new List<System.Data.Odbc.OdbcParameter>(), user, "uts-archival", config, null!, "correlation-id");

        Assert.Same(user, result);
        Assert.Equal([UpdateSp], sut.Calls.Select(c => c.ProcedureName));
    }

    [Fact]
    public async Task ReplaceAsync_WithPatchRowsFromHandler_SendsValuesAsMapped()
    {
        // ReplaceUserV4 falls back to the PATCH rows (no PUT rows in MgtPortal); no PATCH is captured for a PUT
        var (sut, _) = CreateUpdateSut();
        var config = CreateUpdateConfig();
        var user = PutUser(active: false);
        user.ElectronicMailAddresses = [];
        var patchRows = config.UserAttributeSchemas.Where(a => a.HttpRequestType == HttpRequestTypes.PATCH).ToList();

        var payload = await sut.MapAndPreparePayloadAsync(patchRows, user, config);
        await sut.ReplaceAsync(payload, user, config, "correlation-id");

        var call = Assert.Single(sut.Calls);
        Assert.Equal(["@LoginID", "@Name", "@LevelsID", "@Email", "@UpdatedBy", "@Flag"], call.Inputs.Select(p => p.ParameterName));
        Assert.Equal("Bob Is Amazing", ValueOf(call, "@Name"));
        Assert.Equal(DBNull.Value, ValueOf(call, "@Email")); // missing → unchanged, not cleared
        Assert.Equal(2, ValueOf(call, "@Flag"));
    }
}
