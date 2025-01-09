namespace KN.KloudIdentity.Mapper.Domain.As400;

public record As400Group
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string UniqueName { get; init; }
}