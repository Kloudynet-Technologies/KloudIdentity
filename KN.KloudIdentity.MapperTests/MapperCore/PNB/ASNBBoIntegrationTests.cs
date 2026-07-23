using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Moq;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

/// <summary>
/// Tests for <see cref="ASNBBoIntegration"/> — verifies the override reshapes the comma-separated
/// <c>reports</c> value produced by the base mapping into a primitive JSON string array.
/// </summary>
public class ASNBBoIntegrationTests
{
    // urnPrefix hardcoded in JSONParserUtilV2.Parse — DestinationField must start with it.
    private const string UrnPrefix = "urn:kn:ki:schema:";

    private static ASNBBoIntegration CreateSut()
    {
        var mockAuthContext = new Mock<IAuthContext>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockConfiguration = new Mock<IConfiguration>();
        var mockLogger = new Mock<IKloudIdentityLogger>();
        var mockOptions = new Mock<IOptions<AppSettings>>();
        mockOptions.Setup(x => x.Value).Returns(new AppSettings());

        return new ASNBBoIntegration(
            mockAuthContext.Object,
            mockHttpClientFactory.Object,
            mockConfiguration.Object,
            mockLogger.Object,
            mockOptions.Object);
    }

    // Maps a constant value straight to the top-level "reports" field as a String,
    // so the base builder emits {"reports":"<csv>"} for the override to reshape.
    private static AttributeSchema ReportsConstant(string csv) => new AttributeSchema
    {
        DestinationField = UrnPrefix + "reports",
        MappingType = MappingTypes.Constant,
        DestinationType = AttributeDataTypes.String,
        SourceValue = csv
    };

    private static AttributeSchema Constant(string field, string value) => new AttributeSchema
    {
        DestinationField = UrnPrefix + field,
        MappingType = MappingTypes.Constant,
        DestinationType = AttributeDataTypes.String,
        SourceValue = value
    };

    private static string?[] ReportsArray(JObject payload) =>
        (payload["reports"] as JArray)?.Select(t => t.Value<string>()).ToArray()
        ?? throw new Xunit.Sdk.XunitException("'reports' was not a JArray.");

    // 1 - CSV string is split into a multi-element array.
    [Fact]
    public async Task MapAndPreparePayload_SplitsCsv_IntoArray()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema> { ReportsConstant("PAC01A,PAC01B") };

        var result = await sut.MapAndPreparePayloadAsync(schema, new Core2EnterpriseUser { Identifier = "u1" });
        var payload = (JObject)result;

        Assert.Equal(new[] { "PAC01A", "PAC01B" }, ReportsArray(payload));
    }

    // 2 - A single code produces a single-element array.
    [Fact]
    public async Task MapAndPreparePayload_SingleValue_ProducesSingleElementArray()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema> { ReportsConstant("PAC01A") };

        var result = await sut.MapAndPreparePayloadAsync(schema, new Core2EnterpriseUser { Identifier = "u1" });
        var payload = (JObject)result;

        Assert.Equal(new[] { "PAC01A" }, ReportsArray(payload));
    }

    // 3 - Whitespace around codes is trimmed.
    [Fact]
    public async Task MapAndPreparePayload_TrimsWhitespace()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema> { ReportsConstant("PAC01A, PAC01B ,PAC02C") };

        var result = await sut.MapAndPreparePayloadAsync(schema, new Core2EnterpriseUser { Identifier = "u1" });
        var payload = (JObject)result;

        Assert.Equal(new[] { "PAC01A", "PAC01B", "PAC02C" }, ReportsArray(payload));
    }

    // 4 - Empty input yields an empty array (agreed default behavior).
    [Fact]
    public async Task MapAndPreparePayload_Empty_ProducesEmptyArray()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema> { ReportsConstant("") };

        var result = await sut.MapAndPreparePayloadAsync(schema, new Core2EnterpriseUser { Identifier = "u1" });
        var payload = (JObject)result;

        Assert.Empty(ReportsArray(payload));
    }

    // 5 - No reports mapping: payload passes through unchanged, no exception.
    [Fact]
    public async Task MapAndPreparePayload_NoReportsField_ReturnsPayloadUnchanged()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema> { Constant("userName", "jdoe") };

        var result = await sut.MapAndPreparePayloadAsync(schema, new Core2EnterpriseUser { Identifier = "u1" });
        var payload = (JObject)result;

        Assert.Null(payload["reports"]);
        Assert.Equal("jdoe", payload["userName"]!.Value<string>());
    }

    // 6 - reports is already an array (base built it via Array mapping): not double-processed.
    [Fact]
    public async Task MapAndPreparePayload_AlreadyArray_NotDoubleProcessed()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema
            {
                DestinationField = UrnPrefix + "reports",
                MappingType = MappingTypes.Direct,
                DestinationType = AttributeDataTypes.Array,
                ArrayDataType = AttributeDataTypes.String,
                SourceValue = "KIExtension:ExtensionAttribute1"
            }
        };
        var resource = new Core2EnterpriseUser { Identifier = "u1" };
        resource.KIExtension.ExtensionAttribute1 = "PAC01A";

        var result = await sut.MapAndPreparePayloadAsync(schema, resource);
        var payload = (JObject)result;

        // Base wrapped the scalar into ["PAC01A"]; override must leave it as-is.
        Assert.Equal(new[] { "PAC01A" }, ReportsArray(payload));
    }

    // 7 - Non-reports fields are untouched by the transform.
    [Fact]
    public async Task MapAndPreparePayload_LeavesOtherFieldsIntact()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema>
        {
            Constant("userName", "jdoe"),
            ReportsConstant("PAC01A,PAC01B")
        };

        var result = await sut.MapAndPreparePayloadAsync(schema, new Core2EnterpriseUser { Identifier = "u1" });
        var payload = (JObject)result;

        Assert.Equal("jdoe", payload["userName"]!.Value<string>());
        Assert.Equal(new[] { "PAC01A", "PAC01B" }, ReportsArray(payload));
    }
}
