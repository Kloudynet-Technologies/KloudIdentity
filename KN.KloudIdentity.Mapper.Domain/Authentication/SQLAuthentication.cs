namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record SQLAuthentication
{
    public required Guid Id { get; init; }
    public required string AppId { get; init; }
    public required string Driver { get; init; }
    public required string Server { get; init; }
    public required string Database { get; init; }
    public required string UID { get; init; }
    public required string PWD { get; init; }
    public Dictionary<string, string>? AdditionalProperties { get; init; }
}
