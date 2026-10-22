using System.Data.Odbc;
using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.SQL;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

public partial class UTSArchivalSQLIntegrationTests
{
    /// <summary>
    /// Row shaped like the result set of usp_GetUTSArchivalUser.
    /// </summary>
    private static Dictionary<string, object?> GetRow(int? flag = 1, int? levelsId = 3, string? email = "siti@pnb.com") =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["userId"] = "ASNB9998",
            ["username"] = "ASNB9998",
            ["firstName"] = "Siti Binti Omar",
            ["lastName"] = null,
            ["displayName"] = "Siti Binti Omar",
            ["email"] = email,
            ["status"] = "Active",
            ["Flag"] = flag,
            ["LevelsID"] = levelsId,
            ["role"] = "System Admin",
            ["ReferenceNo"] = "9998",
            ["Description"] = "Staff Number"
        };

    [Fact]
    public async Task GetAsync_Success_MapsUserRow()
    {
        var sut = CreateSut(Status(0, GetRow()));

        var user = await sut.GetAsync("ASNB9998", CreateCreateConfig(), "correlation-id");

        Assert.Equal("ASNB9998", user.Identifier);
        Assert.Equal("ASNB9998", user.UserName);
        Assert.Equal("Siti Binti Omar", user.DisplayName);
        Assert.Equal("Siti Binti Omar", user.Name.GivenName);
        Assert.True(user.Active);

        var email = Assert.Single(user.ElectronicMailAddresses);
        Assert.Equal("siti@pnb.com", email.Value);
        Assert.Equal(ElectronicMailAddress.Work, email.ItemType);
        Assert.True(email.Primary);

        var role = Assert.Single(user.Roles);
        Assert.Equal("3", role.Value);
        Assert.Equal("System Admin", role.Display);
    }

    [Fact]
    public async Task GetAsync_CallsGetProcedureWithLoginId()
    {
        var sut = CreateSut(Status(0, GetRow()));

        await sut.GetAsync(" ASNB9998 ", CreateCreateConfig(), "correlation-id");

        var call = Assert.Single(sut.Calls);
        Assert.Equal("dbo.usp_GetUTSArchivalUser", call.ProcedureName);
        var input = Assert.Single(call.Inputs);
        Assert.Equal("@LoginID", input.ParameterName);
        Assert.Equal("ASNB9998", input.Value);
        Assert.Equal(OdbcType.NVarChar, input.OdbcType);

        // GET has no own marker rows; the POST markers are reused
        Assert.Equal(["@ResponseCode", "@ResponseMessage"], call.Outputs.Select(o => o.ParameterName));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(98, true)]
    [InlineData(99, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public async Task GetAsync_DerivesActiveFromFlag(int flag, bool expectedActive)
    {
        var sut = CreateSut(Status(0, GetRow(flag: flag)));

        var user = await sut.GetAsync("ASNB9998", CreateCreateConfig(), "correlation-id");

        Assert.Equal(expectedActive, user.Active);
    }

    [Fact]
    public async Task GetAsync_NullEmailAndRole_AreOmitted()
    {
        var sut = CreateSut(Status(0, GetRow(levelsId: null, email: " ")));

        var user = await sut.GetAsync("ASNB9998", CreateCreateConfig(), "correlation-id");

        Assert.True(user.ElectronicMailAddresses is null || !user.ElectronicMailAddresses.Any());
        Assert.True(user.Roles is null || !user.Roles.Any());
    }

    [Fact]
    public async Task GetAsync_NotFound_Throws404()
    {
        var sut = CreateSut(Status(2));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.GetAsync("ASNB0000", CreateCreateConfig(), "correlation-id"));

        Assert.Equal(HttpStatusCode.NotFound, exception.Response.StatusCode);
    }

    [Fact]
    public async Task GetAsync_SuccessWithoutRow_Throws404()
    {
        var sut = CreateSut(Status(0));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.GetAsync("ASNB9998", CreateCreateConfig(), "correlation-id"));

        Assert.Equal(HttpStatusCode.NotFound, exception.Response.StatusCode);
    }

    [Fact]
    public async Task GetAsync_SystemError_Throws500()
    {
        var sut = CreateSut(Status(4));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.GetAsync("ASNB9998", CreateCreateConfig(), "correlation-id"));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.Response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAsync_EmptyIdentifier_ThrowsBeforeCallingProcedure(string identifier)
    {
        var sut = CreateSut(Status(0, GetRow()));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.GetAsync(identifier, CreateCreateConfig(), "correlation-id"));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task GetAsync_V2Overload_UsesUtsGet()
    {
        var sut = CreateSut(Status(0, GetRow()));

        var user = await sut.GetAsync("ASNB9998", CreateCreateConfig(), null!, "correlation-id");

        Assert.Equal("ASNB9998", user.Identifier);
    }
}
