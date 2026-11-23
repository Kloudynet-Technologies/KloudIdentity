using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace KN.KloudIdentity.MapperTests.Hosting;

/// <summary>
/// #421: an in-memory App Configuration store behind the real App Configuration library, so a test exercises the real
/// selection and sentinel watch without a network. It answers the two requests the library makes (seen against the
/// App Configuration emulator): <c>GET /kv?key=…&amp;label=…</c> (load and reload) and <c>GET /kv/{key}?label=…</c> with
/// <c>If-None-Match</c> (the sentinel check: 304 unchanged, 200 changed, 404 when the store has no such key under that
/// label). "No label" is the library's <see cref="LabelFilter.Null"/>. A key can be a Key Vault reference
/// (<see cref="SetKeyVaultReference"/>).
/// </summary>
internal sealed class FakeAppConfigurationStore : HttpMessageHandler
{
    private readonly ConcurrentDictionary<(string Key, string Label), (string Value, string ETag)> _items = new();
    private readonly ConcurrentDictionary<string, bool> _keyVaultReferences = new();

    /// <summary>Stores <paramref name="key"/> as a Key Vault reference to <paramref name="secretUri"/> (resolved by the
    /// library through its secret resolver, as it resolves a real one through Key Vault).</summary>
    public void SetKeyVaultReference(string key, string label, string secretUri)
    {
        _keyVaultReferences[key] = true;
        Set(key, label, $"{{\"uri\":\"{secretUri}\"}}");
    }
    private readonly ConcurrentQueue<string> _requests = new();

    /// <summary>"key|label" of every single-key request (the sentinel checks).</summary>
    public IReadOnlyList<string> KeyRequests => _requests.ToList();

    public void Set(string key, string label, string value) => _items[(key, label)] = (value, Guid.NewGuid().ToString("N"));

    /// <summary>Points <paramref name="options"/> at this store.</summary>
    public AzureAppConfigurationOptions Connect(AzureAppConfigurationOptions options) =>
        options.Connect($"Endpoint=https://fake-store.azconfig.io;Id=fake;Secret={Convert.ToBase64String(new byte[32])}")
            .ConfigureClientOptions(client => client.Transport = new HttpClientTransport(new HttpClient(this)));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var label = query["label"] ?? LabelFilter.Null;
        var path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);

        if (path == "/kv")
        {
            var prefix = (query["key"] ?? "*").TrimEnd('*');
            var items = _items.Where(i => i.Key.Label == label && i.Key.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(i => Item(i.Key.Key, i.Key.Label, i.Value.Value, i.Value.ETag));
            return Task.FromResult(Json(new { items }, "application/vnd.microsoft.appconfig.kvset+json"));
        }

        var key = path["/kv/".Length..];
        _requests.Enqueue($"{key}|{label}");
        if (!_items.TryGetValue((key, label), out var item))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        if (request.Headers.IfNoneMatch.Any(t => t.Tag.Trim('"') == item.ETag))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        var response = Json(Item(key, label, item.Value, item.ETag), "application/vnd.microsoft.appconfig.kv+json");
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue($"\"{item.ETag}\"");
        return Task.FromResult(response);
    }

    private object Item(string key, string label, string value, string etag) => new
    {
        etag, key, label = label == LabelFilter.Null ? null : label, content_type = _keyVaultReferences.ContainsKey(key) ? "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8" : null, value,
        tags = new Dictionary<string, string>(), locked = false, last_modified = DateTimeOffset.UtcNow.ToString("o"),
    };

    private static HttpResponseMessage Json(object body, string contentType) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, contentType),
    };
}
