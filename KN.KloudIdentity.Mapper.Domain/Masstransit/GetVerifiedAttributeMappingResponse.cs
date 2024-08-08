namespace KN.KI.RabbitMQ.MessageContracts
{
    public class GetVerifiedAttributeMappingResponse : IInterserviceResponseMsg
    {
        public required string Message { get; set; }
        public string? CorrelationId { get; set; }
        public bool? IsError { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? ExceptionDetails { get; set; }
    }
}
