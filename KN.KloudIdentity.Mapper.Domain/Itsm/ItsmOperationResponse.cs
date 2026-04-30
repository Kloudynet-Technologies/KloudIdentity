namespace KN.KloudIdentity.Mapper.Domain.Itsm;

public class ItsmOperationResponse
{
    public string? Id { get; set; }
    public string? ExternalKey { get; set; }
    public string? ApplicationId { get; set; }
    public string? TenantId { get; set; }
    public string? Status { get; set; }
    public string? OperationType { get; set; }
    public string? Email { get; set; }
    public string? TicketId { get; set; }
    public string? LastTicketStatus { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? OperationStatus { get; set; }
}

public enum OperationStatus
{
    Accepted,
    InProgress,
    Completed,
    Failed
}
