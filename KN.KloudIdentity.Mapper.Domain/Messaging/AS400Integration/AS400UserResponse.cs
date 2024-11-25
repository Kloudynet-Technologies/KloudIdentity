namespace KN.KloudIdentity.Mapper.Domain.Messaging.AS400Integration;

public record AS400UserResponse
{
    public required string Identifier { get; set; }
    public required string Username { get; set; }
    public bool Disabled { get; set; }
}