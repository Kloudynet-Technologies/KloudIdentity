namespace KN.KloudIdentity.Mapper.Domain.Messaging.AS400Integration;

public record AS400RequestMessage(string Host, string Username, string Password, string Payload);
