using System.Data.Odbc;
using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

public partial class UTSArchivalSQLIntegrationTests
{
    private const string DeleteSp = "dbo.usp_DeleteUTSArchivalUser";

    [Theory]
    [InlineData(0)]
    [InlineData(3)] // already disabled → idempotent
    public async Task DeleteAsync_SuccessOrAlreadyDisabled_Completes(int responseCode)
    {
        var sut = CreateSut(Status(responseCode));

        await sut.DeleteAsync("ASNB9999", CreateCreateConfig(), "correlation-id");

        Assert.Equal([DeleteSp], sut.Calls.Select(c => c.ProcedureName));
    }

    [Theory]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(2, HttpStatusCode.NotFound)]
    [InlineData(4, HttpStatusCode.InternalServerError)]
    public async Task DeleteAsync_Failure_ThrowsMappedHttpStatus(int responseCode, HttpStatusCode expected)
    {
        var sut = CreateSut(Status(responseCode));

        var exception = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.DeleteAsync("ASNB9999", CreateCreateConfig(), "correlation-id"));

        Assert.Equal(expected, exception.Response.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_SendsOnlyLoginIdWithPostMarkers()
    {
        var sut = CreateSut(Status(0));

        await sut.DeleteAsync(" ASNB9999 ", CreateCreateConfig(), "correlation-id");

        var call = Assert.Single(sut.Calls);
        var input = Assert.Single(call.Inputs);
        Assert.Equal("@LoginID", input.ParameterName);
        Assert.Equal("ASNB9999", input.Value);
        Assert.Equal(OdbcType.NVarChar, input.OdbcType);

        // DELETE has no own marker rows; the POST markers are reused. @DeletedBy uses the SP default.
        Assert.Equal(["@ResponseCode", "@ResponseMessage"], call.Outputs.Select(o => o.ParameterName));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotNeedIdentifierMappingRow()
    {
        // The UTS app has no row with SourceValue "Identifier" (the base SQLIntegration lookup)
        var config = CreateCreateConfig();
        Assert.DoesNotContain(config.UserAttributeSchemas, a => a.SourceValue == "Identifier");
        var sut = CreateSut(Status(0));

        await sut.DeleteAsync("ASNB9999", config, "correlation-id");

        Assert.Single(sut.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAsync_EmptyIdentifier_ThrowsBeforeCallingProcedure(string identifier)
    {
        var sut = CreateSut(Status(0));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeleteAsync(identifier, CreateCreateConfig(), "correlation-id"));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task DeleteAsync_NoDeleteSpName_ThrowsArgumentException()
    {
        var sut = CreateSut(Status(0));
        var config = CreateCreateConfig();
        config.IntegrationDetails = """{"AppId":"uts-archival","PostSpName":"dbo.usp_CreateUTSArchivalUser","GetSpName":"dbo.usp_GetUTSArchivalUser"}""";

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAsync("ASNB9999", config, "correlation-id"));
        Assert.Empty(sut.Calls);
    }

    [Fact]
    public async Task DeleteAsync_V2Overload_UsesUtsDelete()
    {
        var sut = CreateSut(Status(3));

        await sut.DeleteAsync("ASNB9999", "uts-archival", CreateCreateConfig(), null!, "correlation-id");

        Assert.Equal([DeleteSp], sut.Calls.Select(c => c.ProcedureName));
    }
}
