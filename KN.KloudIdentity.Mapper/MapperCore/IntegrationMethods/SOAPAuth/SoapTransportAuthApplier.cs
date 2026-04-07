using System.Net;
using System.Net.Http.Headers;
using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.MapperCore;

public sealed class SoapTransportAuthApplier : ISoapAuthApplier
{
    public Task ApplyAsync(SoapAuthContext context, CancellationToken cancellationToken = default)
    {
        var transport = context.AuthOptions?.Transport;
        if (transport?.Enabled == true && transport.UseNtlm)
        {
            if (context.Handler == null)
            {
                throw new InvalidOperationException("SOAP NTLM authentication requires an HttpClientHandler.");
            }

            if (transport.UseDefaultCredentials)
            {
                context.Handler.UseDefaultCredentials = true;
                context.Handler.Credentials = CredentialCache.DefaultNetworkCredentials;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(transport.Username) || string.IsNullOrWhiteSpace(transport.Password))
                {
                    throw new InvalidOperationException("SOAP NTLM authentication requires username and password when UseDefaultCredentials is false.");
                }

                context.Handler.Credentials = new NetworkCredential(transport.Username, transport.Password, transport.Domain);
            }
        }

        var tokenPlacement = context.AuthOptions?.TokenPlacement;
        if (tokenPlacement?.Enabled == true)
        {
            if (tokenPlacement.UseAuthorizationHeader && !string.IsNullOrWhiteSpace(context.Token))
            {
                context.Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.Token);
            }

            if (tokenPlacement.CustomHttpHeaders != null)
            {
                foreach (var header in tokenPlacement.CustomHttpHeaders)
                {
                    var value = header.Value;
                    if (!string.IsNullOrWhiteSpace(context.Token))
                    {
                        value = value.Replace(tokenPlacement.TokenPlaceholder, context.Token, StringComparison.Ordinal);
                    }

                    context.Request.Headers.TryAddWithoutValidation(header.Key, value);
                }
            }
        }

        return Task.CompletedTask;
    }
}
