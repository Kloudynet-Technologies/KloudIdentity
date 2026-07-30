using Xunit;

namespace KN.KloudIdentity.MapperTests;

/// <summary>
/// JSONParserUtilV2&lt;Core2EnterpriseUser&gt; exposes a shared static field that JSONParserUtilTests
/// mutates (via Parse(..., isSamplePayload: true)) and SQLIntegrationTest reads indirectly
/// (via SQLIntegration.MapAndPreparePayloadAsync). Grouping both classes into this collection
/// stops xUnit from running them in parallel, avoiding a cross-test race on that static state.
/// </summary>
[CollectionDefinition("JsonParserV2SharedState")]
public class JsonParserV2SharedStateCollection
{
}
