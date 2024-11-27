namespace KN.KloudIdentity.Mapper.Domain.Messaging.AS400Integration;

public record AS400UserResponse
{
    public required string CorrelationId { get; set; }
    public required List<ResponsePayload> ResponsePayload { get; set; }
    public bool IsSuccessful { get; set; }
    public string? Error { get; set; }
}

public class ResponsePayload
{
    public required string Identifier { get; set; }
    public required string Username { get; set; }
    public bool Disabled { get; set; }
}
