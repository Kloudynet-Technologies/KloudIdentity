namespace KN.KI.RabbitMQ.MessageContracts
{
    public class MgtPortalServiceRequestMsg : IMgtPortalServiceRequestMsg
    {
        public string Message { get; set; }
        public string Action { get; set; }
        public string? CorrelationId { get; set; }
        public string? ReplyTo { get; set; }

        public MgtPortalServiceRequestMsg(string message, string action, string? correlationId, string? replyTo)
        {
            Message = message;
            Action = action;
            CorrelationId = correlationId;
            ReplyTo = replyTo;
        }
    }
}
