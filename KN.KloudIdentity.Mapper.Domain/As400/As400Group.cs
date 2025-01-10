namespace KN.KloudIdentity.Mapper.Domain.As400;

public record As400Group
{
    public required string Id { get; init; }
    public string? GroupId { get; init; }
    public required string AppId { get; init; }
    public required string GroupName { get; init; }
    public string? UniqueName { get; init; }
}