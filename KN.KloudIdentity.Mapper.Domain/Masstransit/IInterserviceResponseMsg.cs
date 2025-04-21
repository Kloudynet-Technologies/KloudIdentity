namespace KN.KI.RabbitMQ.MessageContracts
{
    public interface IInterserviceResponseMsg
    {
        string Message { get; set; }

        string? CorrelationId { get; set; }

        bool? IsError { get; set; }

        string? ErrorMessage { get; set; }

        Exception? ExceptionDetails { get; set; }
    }
}
