namespace KN.KI.RabbitMQ.MessageContracts;

public class MetaverseServiceRequestMsg : IMetaverseServiceRequestMsg
{
    public string Message { get; set; }
    public string Action { get; set; }
    public string? CorrelationId { get; set; }
    public string? ReplyTo { get; set; }
    
    public MetaverseServiceRequestMsg(string message, string action, string? correlationId, string? replyTo)
    {
        Message = message;
        Action = action;
        CorrelationId = correlationId;
        ReplyTo = replyTo;
    }
}