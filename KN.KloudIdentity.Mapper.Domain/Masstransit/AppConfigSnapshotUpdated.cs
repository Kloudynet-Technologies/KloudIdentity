namespace KN.KI.RabbitMQ.MessageContracts;

public class AppConfigSnapshotUpdated : IAppConfigSnapshotUpdated
{
    public required string AppId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public required string Message { get; set; }
    public string? Action { get; set; }
    public string? CorrelationId { get; set; }
    public string? ReplyTo { get; set; }
    public required string PerformedBy { get; set; }
}