using System;

namespace KN.KloudIdentity.Mapper.Domain.Messaging;

public class StagingQueueResponseMessage
{
    public Guid CorrelationId { get; }

    public dynamic Message { get; }

    public bool? IsError { get; set; }

    public string? ErrorMessage { get; set; }

    public StagingQueueResponseMessage(Guid correlationId, dynamic message)
    {
        CorrelationId = correlationId;
        Message = message;
    }
}
