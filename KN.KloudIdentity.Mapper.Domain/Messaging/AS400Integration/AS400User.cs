namespace KN.KloudIdentity.Mapper.Domain.Messaging.AS400Integration;

public class AS400User
{
    public required string Identifier { get; set; }
    public required string Username { get; set; }
    public bool Disabled { get; set; }
}
