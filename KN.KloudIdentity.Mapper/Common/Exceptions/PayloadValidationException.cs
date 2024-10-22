using System;

namespace KN.KloudIdentity.Mapper.Common.Exceptions;

public class PayloadValidationException(string appId, string[] errorMessages) :
                Exception($"Payload validation failed for {appId}.\n{string.Join(Environment.NewLine, errorMessages)}")
{
}
