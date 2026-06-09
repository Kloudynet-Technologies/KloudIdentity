using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface ISoapAuthApplier
{
    Task ApplyAsync(SoapAuthContext context, CancellationToken cancellationToken = default);
}

public sealed class SoapAuthContext
{
    public required AppConfig AppConfig { get; init; }
    public required SCIMDirections Direction { get; init; }
    public required HttpClient HttpClient { get; init; }
    public required HttpRequestMessage Request { get; init; }
    public HttpClientHandler? Handler { get; init; }
    public string? Token { get; init; }
    public SOAPAuthenticationOptions? AuthOptions { get; init; }
    public required string Payload { get; set; }
}
