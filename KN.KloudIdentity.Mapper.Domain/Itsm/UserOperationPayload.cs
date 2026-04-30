using KN.KloudIdentity.Mapper.Domain.Messaging;

namespace KN.KloudIdentity.Mapper.Domain.Itsm;

public class UserOperationPayload
{
    public required string TenantId { get; set; }
    public required string ApplicationId { get; set; }
    public required string UserKey { get; set; }
    public required OperationTypes OperationType { get; set; }
    public string? RequestedBy { get; set; }

    /// <summary>
    /// JSON-serialized user attribute bag. Shape varies by operation.
    /// </summary>
    public string? Attributes { get; set; }
}