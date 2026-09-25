using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{
    private const string ValidAuthenticationJson =
        """{"Driver":"ODBC Driver 17 for SQL Server","Server":"localhost","Database":"TestDb","UID":"TestUser","PWD":"TestPassword"}""";

    #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
    private static AppConfig CreateAppConfig(dynamic? authenticationDetails = null, dynamic? integrationDetails = null) => new()
    {
        UserURIs = null,
        UserAttributeSchemas = null,
        IntegrationMethodOutbound = IntegrationMethods.SQL,
        AuthenticationDetails = authenticationDetails,
        IntegrationDetails = integrationDetails
    };
    #pragma warning restore CS8625

    [Fact]
    public void ConfigureMapperServices_RegistersSqlIntegrationAsIntegrationBaseV2()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.ConfigureMapperServices(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IIntegrationBaseV2) && d.ImplementationType == typeof(SQLIntegration));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IIntegrationBase) && d.ImplementationType == typeof(SQLIntegration));
    }

    [Fact]
    public void IntegrationBaseFactory_WithSqlDefaultMapping_ResolvesSqlIntegration()
    {
        var appSettings = Options.Create(new AppSettings
        {
            IntegrationMappings = new IntegrationMappings
            {
                DefaultIntegration = new Dictionary<string, string>
                {
                    [IntegrationMethods.SQL.ToString()] = nameof(SQLIntegration)
                }
            }
        });

        var factory = new IntegrationBaseFactory(new List<IIntegrationBaseV2> { _odbcIntegration }, appSettings);

        var resolved = factory.GetIntegration(IntegrationMethods.SQL, "sql-app");

        Assert.IsType<SQLIntegration>(resolved);
    }

    [Fact]
    public async Task GetAuthenticationAsync_ShouldReturnOdbcConnection_WhenAuthenticationDetailsAreJsonElement()
    {
        // AppConfigSnapshots are deserialized with System.Text.Json, so dynamic members arrive as JsonElement
        var config = CreateAppConfig(authenticationDetails: JsonDocument.Parse(ValidAuthenticationJson).RootElement);

        var connection = await _odbcIntegration.GetAuthenticationAsync(config, SCIMDirections.Outbound, CancellationToken.None);

        Assert.Equal(
            "Driver=ODBC Driver 17 for SQL Server;Server=localhost;Database=TestDb;Uid=TestUser;Pwd=TestPassword;",
            connection.ConnectionString);
    }

    [Fact]
    public async Task GetAuthenticationAsync_ShouldThrowArgumentNullException_WhenJsonElementIsMissingPassword()
    {
        var json = """{"Driver":"ODBC Driver 17 for SQL Server","Server":"localhost","Database":"TestDb","UID":"TestUser"}""";
        var config = CreateAppConfig(authenticationDetails: JsonDocument.Parse(json).RootElement);

        async Task Act() => await _odbcIntegration.GetAuthenticationAsync(config, SCIMDirections.Outbound, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task GetAuthenticationAsync_ShouldThrowArgumentException_WhenAuthenticationDetailsAreNotJson()
    {
        var config = CreateAppConfig(authenticationDetails: "not-json");

        async Task Act() => await _odbcIntegration.GetAuthenticationAsync(config, SCIMDirections.Outbound, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(Act);
    }

    [Fact]
    public void GetIntegrationDetails_ShouldDeserialize_WhenIntegrationDetailsAreJsonElement()
    {
        var json = """{"AppId":"TestApp","PostSpName":"dbo.usp_Create","GetSpName":"dbo.usp_Get","PatchSpName":"dbo.usp_Update","DeleteSpName":"dbo.usp_Delete"}""";
        var config = CreateAppConfig(integrationDetails: JsonDocument.Parse(json).RootElement);

        var details = TestableSQLIntegration.CallGetIntegrationDetails(config);

        Assert.Equal("dbo.usp_Create", details.PostSpName);
        Assert.Equal("dbo.usp_Get", details.GetSpName);
        Assert.Equal("dbo.usp_Update", details.PatchSpName);
        Assert.Equal("dbo.usp_Delete", details.DeleteSpName);
    }

    [Fact]
    public async Task ProvisionAsync_V2_ShouldDelegateToV1_WhenPayloadIsInvalid()
    {
        var config = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.ProvisionAsync(new { Invalid = true }, "TestApp", config, null!, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task GetAsync_V2_ShouldDelegateToV1_WhenIdentifierIsEmpty()
    {
        var config = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.GetAsync(string.Empty, config, null!, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task UpdateAsync_V2_ShouldDelegateToV1_WhenPayloadIsInvalid()
    {
        var config = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.UpdateAsync(new { Invalid = true }, new Core2EnterpriseUser(), "TestApp", config, null!,
                Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task DeleteAsync_V2_ShouldDelegateToV1_WhenIdentifierIsEmpty()
    {
        var config = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.DeleteAsync(string.Empty, "TestApp", config, null!, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task ReplaceAsync_V2_ShouldThrowNotSupportedException()
    {
        var config = CreateAppConfig();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _odbcIntegration.ReplaceAsync(new object(), new Core2EnterpriseUser(), "TestApp", config, null!,
                Guid.NewGuid().ToString()));
    }

    /// <summary>
    /// Exposes the protected helpers of <see cref="SQLIntegration"/> for testing.
    /// </summary>
    private sealed class TestableSQLIntegration(IOptions<AppSettings> appSettings) : SQLIntegration(appSettings)
    {
        public static Mapper.Domain.SQL.SQLIntegrationDetails CallGetIntegrationDetails(AppConfig appConfig) =>
            GetIntegrationDetails(appConfig);

        public static bool CallIsOutputMapping(AttributeSchema attribute) => IsOutputMapping(attribute);

        public static IReadOnlyList<Mapper.Domain.SQL.SqlOutputMapping> CallGetOutputMappings(
            IEnumerable<AttributeSchema> schema) => GetOutputMappings(schema);

        public static string CallToSqlDeclarationType(AttributeDataTypes dataType) => ToSqlDeclarationType(dataType);

        public static IReadOnlyList<Mapper.Domain.SQL.SqlOutputMapping> CallGetOutputMappings(AppConfig appConfig,
            HttpRequestTypes requestType) => GetOutputMappings(appConfig, requestType);

        public static string CallBuildProcedureBatch(string procedureName,
            IReadOnlyList<System.Data.Odbc.OdbcParameter> inputs,
            IReadOnlyList<Mapper.Domain.SQL.SqlOutputMapping> outputs) =>
            BuildProcedureBatch(procedureName, inputs, outputs);

        public static Task<Mapper.Domain.SQL.SqlProcedureResult> CallReadProcedureResultAsync(
            System.Data.Common.DbDataReader reader, IReadOnlyList<Mapper.Domain.SQL.SqlOutputMapping> outputs) =>
            ReadProcedureResultAsync(reader, outputs, CancellationToken.None);

        public void CallThrowIfFailed(Mapper.Domain.SQL.SqlProcedureResult result, AppConfig appConfig) =>
            ThrowIfFailed(result, "dbo.usp_Test", appConfig, "correlation-id");
    }
}
