namespace KN.KloudIdentity.Mapper.Domain;

public record InterserviceMessage(string Message, string CorrelationId, bool IsError = false, Exception Exception = null, string Action = "");