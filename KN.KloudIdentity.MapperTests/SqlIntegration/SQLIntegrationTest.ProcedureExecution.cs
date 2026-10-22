using System.Data;
using System.Data.Odbc;
using System.Net;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using KN.KloudIdentity.Mapper.MapperCore;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{
    private static readonly SqlOutputMapping ResponseCodeOutput = new("@ResponseCode", SQLIntegration.ResponseCodeRole, AttributeDataTypes.Int, "INT");
    private static readonly SqlOutputMapping ResponseMessageOutput = new("@ResponseMessage", SQLIntegration.ResponseMessageRole, AttributeDataTypes.String, "NVARCHAR(4000)");

    private static OdbcParameter Input(string name, object? value) => new(name, OdbcType.NVarChar) { Value = value ?? DBNull.Value };

    private static DataTable Table(string[] columns, params object?[][] rows)
    {
        var table = new DataTable();
        foreach (var column in columns)
            table.Columns.Add(column, typeof(object));
        foreach (var row in rows)
            table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return table;
    }

    #region BuildProcedureBatch

    [Fact]
    public void BuildProcedureBatch_ShouldEmitNamedInputsAndOutputs()
    {
        var inputs = new[] { Input("@LoginID", "ASNB9999"), Input("@Name", "Ali") };
        var outputs = new[] { ResponseCodeOutput, ResponseMessageOutput };

        var batch = TestableSQLIntegration.CallBuildProcedureBatch("dbo.usp_CreateUTSArchivalUser", inputs, outputs);

        Assert.Equal(
            "SET NOCOUNT ON;\n" +
            "DECLARE @o0 INT, @o1 NVARCHAR(4000);\n" +
            "EXEC dbo.usp_CreateUTSArchivalUser @LoginID = ?, @Name = ?, @ResponseCode = @o0 OUTPUT, @ResponseMessage = @o1 OUTPUT;\n" +
            "SELECT @o0 AS [o0], @o1 AS [o1];",
            batch);
        Assert.Equal(inputs.Length, batch.Count(c => c == '?'));
    }

    [Fact]
    public void BuildProcedureBatch_ShouldOmitDeclareAndSelect_WhenNoOutputs()
    {
        var batch = TestableSQLIntegration.CallBuildProcedureBatch("[dbo].[usp_Get]", new[] { Input("@LoginID", "x") }, []);

        Assert.Equal("SET NOCOUNT ON;\nEXEC [dbo].[usp_Get] @LoginID = ?;\n", batch);
    }

    [Theory]
    [InlineData("@Name = 1; --")]
    [InlineData("Name")]
    [InlineData("@Na me")]
    [InlineData("")]
    public void BuildProcedureBatch_ShouldThrow_WhenInputNameIsMalformed(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            TestableSQLIntegration.CallBuildProcedureBatch("dbo.usp_Create", new[] { Input(name, "x") }, []));
    }

    [Fact]
    public void BuildProcedureBatch_ShouldThrow_WhenOutputNameIsMalformed()
    {
        var output = ResponseCodeOutput with { ParameterName = "@ResponseCode OUTPUT; DROP TABLE Users; --" };

        Assert.Throws<ArgumentException>(() =>
            TestableSQLIntegration.CallBuildProcedureBatch("dbo.usp_Create", [], new[] { output }));
    }

    [Fact]
    public void BuildProcedureBatch_ShouldThrow_WhenInputNameIsDuplicated()
    {
        Assert.Throws<ArgumentException>(() =>
            TestableSQLIntegration.CallBuildProcedureBatch("dbo.usp_Create",
                new[] { Input("@Name", "a"), Input("@name", "b") }, []));
    }

    [Fact]
    public void BuildProcedureBatch_ShouldThrow_WhenNameIsBothInputAndOutput()
    {
        Assert.Throws<ArgumentException>(() =>
            TestableSQLIntegration.CallBuildProcedureBatch("dbo.usp_Create",
                new[] { Input("@ResponseCode", "0") }, new[] { ResponseCodeOutput }));
    }

    [Theory]
    [InlineData("dbo.usp_Create; DROP TABLE Users")]
    [InlineData("usp Create")]
    [InlineData("")]
    public void BuildProcedureBatch_ShouldThrow_WhenProcedureNameIsInvalid(string procedureName)
    {
        Assert.Throws<ArgumentException>(() =>
            TestableSQLIntegration.CallBuildProcedureBatch(procedureName, new[] { Input("@Name", "x") }, []));
    }

    #endregion

    #region ReadProcedureResultAsync

    [Fact]
    public async Task ReadProcedureResultAsync_ShouldReturnRowAndStatus_WhenProcedureReturnsRow()
    {
        using var reader = new DataTableReader(new[]
        {
            Table(["LoginID", "Name", "Email"], ["ASNB9999", "Ali", null]),
            Table(["o0", "o1"], [0, "Success"])
        });

        var result = await TestableSQLIntegration.CallReadProcedureResultAsync(reader, new[] { ResponseCodeOutput, ResponseMessageOutput });

        Assert.Equal(0, result.ResponseCode);
        Assert.Equal("Success", result.ResponseMessage);
        Assert.NotNull(result.Row);
        Assert.Equal("ASNB9999", result.Row!["loginid"]);
        Assert.Null(result.Row["Email"]);
        Assert.Equal(0, result.Outputs["@ResponseCode"]);
    }

    [Fact]
    public async Task ReadProcedureResultAsync_ShouldReturnStatusOnly_WhenProcedureReturnsEarly()
    {
        using var reader = new DataTableReader(new[] { Table(["o0", "o1"], [3, "User already exists"]) });

        var result = await TestableSQLIntegration.CallReadProcedureResultAsync(reader, new[] { ResponseCodeOutput, ResponseMessageOutput });

        Assert.Equal(3, result.ResponseCode);
        Assert.Equal("User already exists", result.ResponseMessage);
        Assert.Null(result.Row);
    }

    [Fact]
    public async Task ReadProcedureResultAsync_ShouldThrow_WhenResponseCodeIsNull()
    {
        using var reader = new DataTableReader(new[] { Table(["o0", "o1"], new object?[] { null, null }) });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestableSQLIntegration.CallReadProcedureResultAsync(reader, new[] { ResponseCodeOutput, ResponseMessageOutput }));
    }

    [Fact]
    public async Task ReadProcedureResultAsync_ShouldThrow_WhenOutputRowIsMissing()
    {
        using var reader = new DataTableReader(new[] { Table(["o0"]) });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestableSQLIntegration.CallReadProcedureResultAsync(reader, new[] { ResponseCodeOutput }));
    }

    [Fact]
    public async Task ReadProcedureResultAsync_ShouldReturnLastResultSetAsRow_WhenNoOutputs()
    {
        using var reader = new DataTableReader(new[] { Table(["LoginID"], ["ASNB9999"], ["ASNB0001"]) });

        var result = await TestableSQLIntegration.CallReadProcedureResultAsync(reader, []);

        Assert.Null(result.ResponseCode);
        Assert.Equal("ASNB9999", result.Row!["LoginID"]);
        Assert.Empty(result.Outputs);
    }

    [Fact]
    public async Task ReadProcedureResultAsync_ShouldCapturePlainOutputByParameterName()
    {
        var plainOutput = new SqlOutputMapping("@NewUserId", null, AttributeDataTypes.BigInt, "BIGINT");
        using var reader = new DataTableReader(new[] { Table(["o0", "o1"], [0, 42L]) });

        var result = await TestableSQLIntegration.CallReadProcedureResultAsync(reader, new[] { ResponseCodeOutput, plainOutput });

        Assert.Equal(0, result.ResponseCode);
        Assert.Null(result.ResponseMessage);
        Assert.Equal(42L, result.Outputs["@NewUserId"]);
    }

    #endregion

    #region ThrowIfFailed

    [Theory]
    [InlineData(1, HttpStatusCode.BadRequest)]
    [InlineData(2, HttpStatusCode.NotFound)]
    [InlineData(3, HttpStatusCode.Conflict)]
    [InlineData(4, HttpStatusCode.InternalServerError)]
    [InlineData(99, HttpStatusCode.InternalServerError)]
    public void ThrowIfFailed_ShouldMapResponseCodeToHttpStatus(int responseCode, HttpStatusCode expected)
    {
        var sut = new TestableSQLIntegration(_mockAppSettings.Object);
        var result = new SqlProcedureResult(responseCode, "failure", null, new Dictionary<string, object?>());

        var exception = Assert.Throws<HttpResponseException>(() => sut.CallThrowIfFailed(result, CreateAppConfig()));

        Assert.Equal(expected, exception.Response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public void ThrowIfFailed_ShouldNotThrow_WhenSuccessOrNoResponseCode(int? responseCode)
    {
        var sut = new TestableSQLIntegration(_mockAppSettings.Object);
        var result = new SqlProcedureResult(responseCode, null, null, new Dictionary<string, object?>());

        sut.CallThrowIfFailed(result, CreateAppConfig());
    }

    #endregion

    #region GetOutputMappings(AppConfig, HttpRequestTypes)

    [Fact]
    public void GetOutputMappings_ShouldFallBackToPostRows_WhenOperationHasNoMarkers()
    {
        var config = CreateAppConfig();
        config.UserAttributeSchemas = new List<AttributeSchema>
        {
            new() { HttpRequestType = HttpRequestTypes.POST, SourceValue = "UserName", DestinationField = "@LoginID" },
            new() { HttpRequestType = HttpRequestTypes.POST, SourceValue = "Output:ResponseCode", DestinationField = "@ResponseCode", DestinationType = AttributeDataTypes.Int },
            new() { HttpRequestType = HttpRequestTypes.PATCH, SourceValue = "Output:ResponseCode", DestinationField = "@RetCode", DestinationType = AttributeDataTypes.Int }
        };

        var getOutputs = TestableSQLIntegration.CallGetOutputMappings(config, HttpRequestTypes.GET);
        var patchOutputs = TestableSQLIntegration.CallGetOutputMappings(config, HttpRequestTypes.PATCH);

        Assert.Equal("@ResponseCode", Assert.Single(getOutputs).ParameterName);
        Assert.Equal("@RetCode", Assert.Single(patchOutputs).ParameterName);
    }

    #endregion
}
