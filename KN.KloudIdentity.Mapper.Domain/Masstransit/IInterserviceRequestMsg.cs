namespace KN.KI.RabbitMQ.MessageContracts
{
    public interface IInterserviceRequestMsg
    {
        string Message { get; set; }

        string Action { get; set; }

        string? CorrelationId { get; set; }

        string? ReplyTo { get; set; }
    }
}
