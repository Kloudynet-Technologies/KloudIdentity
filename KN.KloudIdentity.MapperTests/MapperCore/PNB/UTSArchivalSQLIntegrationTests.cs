using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

public partial class UTSArchivalSQLIntegrationTests
{
    private readonly UTSArchivalSQLIntegration _sut = new(Options.Create(new AppSettings()), new PatchOperationContext());

    #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
    private static AppConfig CreateAppConfig(params AttributeSchema[] schemas) => new()
    {
        AppId = "uts-archival",
        UserURIs = null,
        UserAttributeSchemas = schemas.ToList(),
        IntegrationMethodOutbound = IntegrationMethods.SQL,
        AuthenticationDetails = null
    };
    #pragma warning restore CS8625

    private static AttributeSchema Row(HttpRequestTypes requestType, string sourceValue, string destinationField,
        AttributeDataTypes type = AttributeDataTypes.String, MappingTypes mappingType = MappingTypes.Direct) => new()
    {
        HttpRequestType = requestType,
        SourceValue = sourceValue,
        DestinationField = destinationField,
        DestinationType = type,
        MappingType = mappingType
    };

    [Fact]
    public void IntegrationMethod_ShouldBeSql()
    {
        Assert.Equal(IntegrationMethods.SQL, _sut.IntegrationMethod);
        Assert.IsAssignableFrom<IIntegrationBaseV2>(_sut);
    }

    [Fact]
    public void IntegrationBaseFactory_WithAppIdMapping_ResolvesUtsAndDefaultsOtherSqlApps()
    {
        var appSettings = Options.Create(new AppSettings
        {
            IntegrationMappings = new IntegrationMappings
            {
                AppIdToIntegration = new Dictionary<string, string> { ["uts-archival"] = nameof(UTSArchivalSQLIntegration) },
                DefaultIntegration = new Dictionary<string, string> { [IntegrationMethods.SQL.ToString()] = nameof(SQLIntegration) }
            }
        });
        var factory = new IntegrationBaseFactory(
            new List<IIntegrationBaseV2> { new SQLIntegration(appSettings), _sut }, appSettings);

        Assert.IsType<UTSArchivalSQLIntegration>(factory.GetIntegration(IntegrationMethods.SQL, "uts-archival"));
        Assert.IsType<SQLIntegration>(factory.GetIntegration(IntegrationMethods.SQL, "other-sql-app"));
    }

    #region ResolveLookupParameter

    [Fact]
    public void ResolveLookupParameter_ShouldReturnPostUserNameDestination()
    {
        var config = CreateAppConfig(
            Row(HttpRequestTypes.PATCH, "UserName", "@PatchLogin"),
            Row(HttpRequestTypes.POST, "DisplayName", "@Name"),
            Row(HttpRequestTypes.POST, "UserName", "urn:kn:ki:schema:@LoginID"));

        Assert.Equal("@LoginID", TestableUTSArchivalSQLIntegration.CallResolveLookupParameter(config));
    }

    [Fact]
    public void ResolveLookupParameter_ShouldThrow_WhenUserNameIsNotMappedOnPost()
    {
        var config = CreateAppConfig(
            Row(HttpRequestTypes.PATCH, "UserName", "@LoginID"),
            Row(HttpRequestTypes.POST, "DisplayName", "@Name"),
            Row(HttpRequestTypes.POST, "", "@ReferenceNo"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestableUTSArchivalSQLIntegration.CallResolveLookupParameter(config));
        Assert.Contains("uts-archival", exception.Message);
    }

    #endregion

    #region ResolveStatusOutputs

    [Fact]
    public void ResolveStatusOutputs_ShouldReturnOperationMarkers()
    {
        var config = CreateAppConfig(
            Row(HttpRequestTypes.POST, "Output:ResponseCode", "@ResponseCode", AttributeDataTypes.Int, MappingTypes.Constant),
            Row(HttpRequestTypes.PATCH, "Output:ResponseCode", "@ResponseCode", AttributeDataTypes.Int, MappingTypes.Constant),
            Row(HttpRequestTypes.PATCH, "Output:ResponseMessage", "@ResponseMessage", AttributeDataTypes.String, MappingTypes.Constant));

        var outputs = TestableUTSArchivalSQLIntegration.CallResolveStatusOutputs(config, HttpRequestTypes.PATCH);

        Assert.Collection(outputs,
            o => Assert.Equal(SQLIntegration.ResponseCodeRole, o.Role),
            o => Assert.Equal(SQLIntegration.ResponseMessageRole, o.Role));
    }

    [Theory]
    [InlineData(HttpRequestTypes.GET)]
    [InlineData(HttpRequestTypes.DELETE)]
    public void ResolveStatusOutputs_ShouldFallBackToPostMarkers_ForGetAndDelete(HttpRequestTypes requestType)
    {
        var config = CreateAppConfig(
            Row(HttpRequestTypes.POST, "UserName", "@LoginID"),
            Row(HttpRequestTypes.POST, "Output:ResponseCode", "@ResponseCode", AttributeDataTypes.Int, MappingTypes.Constant),
            Row(HttpRequestTypes.POST, "Output:ResponseMessage", "@ResponseMessage", AttributeDataTypes.String, MappingTypes.Constant));

        var outputs = TestableUTSArchivalSQLIntegration.CallResolveStatusOutputs(config, requestType);

        Assert.Equal(2, outputs.Count);
        Assert.Equal("@ResponseCode", outputs[0].ParameterName);
    }

    [Fact]
    public void ResolveStatusOutputs_ShouldSupportRenamedOutputParameter()
    {
        var config = CreateAppConfig(
            Row(HttpRequestTypes.POST, "Output:ResponseCode", "@RetCode", AttributeDataTypes.Int, MappingTypes.Constant));

        var outputs = TestableUTSArchivalSQLIntegration.CallResolveStatusOutputs(config, HttpRequestTypes.POST);

        Assert.Equal("@RetCode", Assert.Single(outputs).ParameterName);
    }

    [Fact]
    public void ResolveStatusOutputs_ShouldThrow_WhenResponseCodeMarkerIsMissing()
    {
        var config = CreateAppConfig(
            Row(HttpRequestTypes.POST, "UserName", "@LoginID"),
            Row(HttpRequestTypes.POST, "Output:ResponseMessage", "@ResponseMessage", AttributeDataTypes.String, MappingTypes.Constant),
            Row(HttpRequestTypes.POST, "Output", "@NewUserId", AttributeDataTypes.BigInt, MappingTypes.Constant));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestableUTSArchivalSQLIntegration.CallResolveStatusOutputs(config, HttpRequestTypes.POST));
        Assert.Contains("Output:ResponseCode", exception.Message);
    }

    #endregion

    /// <summary>
    /// Exposes the protected helpers of <see cref="UTSArchivalSQLIntegration"/> for testing and replaces the
    /// database call with <see cref="Result"/>.
    /// </summary>
    private sealed class TestableUTSArchivalSQLIntegration(IOptions<AppSettings> appSettings,
        PatchOperationContext? patchOperationContext = null)
        : UTSArchivalSQLIntegration(appSettings, patchOperationContext ?? new PatchOperationContext())
    {
        public SqlProcedureResult Result { get; set; } =
            new(0, "Success", null, new Dictionary<string, object?>());

        /// <summary>
        /// Per-procedure results; procedures not listed return <see cref="Result"/>.
        /// </summary>
        public Dictionary<string, SqlProcedureResult> ResultsByProcedure { get; } = [];

        public List<(string ProcedureName, IReadOnlyList<System.Data.Odbc.OdbcParameter> Inputs, IReadOnlyList<SqlOutputMapping> Outputs)> Calls { get; } = [];

        public static string CallResolveLookupParameter(AppConfig appConfig) => ResolveLookupParameter(appConfig);

        public static IReadOnlyList<SqlOutputMapping> CallResolveStatusOutputs(AppConfig appConfig,
            HttpRequestTypes requestType) => ResolveStatusOutputs(appConfig, requestType);

        protected override Task<SqlProcedureResult> ExecuteProcedureAsync(AppConfig appConfig, string procedureName,
            IReadOnlyList<System.Data.Odbc.OdbcParameter> inputs, IReadOnlyList<SqlOutputMapping> outputs,
            string correlationId, CancellationToken cancellationToken)
        {
            Calls.Add((procedureName, inputs, outputs));
            return Task.FromResult(ResultsByProcedure.GetValueOrDefault(procedureName, Result));
        }
    }
}
