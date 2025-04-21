using System;

namespace KN.KloudIdentity.Mapper.Domain.Messaging;

public class StagingQueueRequestMessage
{
    public string CorrelationId { get; init; }

    public dynamic Message { get; init; }

    public HostTypes HostType { get; init; }

    public OperationTypes OperationType { get; init; }

    public StagingQueueRequestMessage(string correlationId, dynamic message, HostTypes hostType, OperationTypes operationType)
    {
        CorrelationId = correlationId;
        Message = message;
        HostType = hostType;
        OperationType = operationType;
    }
}
