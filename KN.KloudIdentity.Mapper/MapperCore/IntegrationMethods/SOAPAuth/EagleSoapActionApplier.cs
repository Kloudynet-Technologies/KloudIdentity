//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.MapperCore;

internal sealed class EagleSoapActionApplier : ISoapAuthApplier
{
    private const string SoapActionValue = "\"RunTaskRequestSync\"";

    public Task ApplyAsync(SoapAuthContext context, CancellationToken cancellationToken = default)
    {
        context.Request.Headers.Remove("SOAPAction");
        context.Request.Headers.TryAddWithoutValidation("SOAPAction", SoapActionValue);
        return Task.CompletedTask;
    }
}
