#nullable enable
using Microsoft.Extensions.Configuration;

namespace Microsoft.SCIM.WebHostSample.Configuration;

/// <summary>
/// #421: the token settings (<c>KI:Token</c>), read once at startup and used both to issue tokens (<c>TokenController</c>)
/// and to validate them (<c>AddJwtBearer</c>, whose options are built once). Before #421 nothing ever reloaded, so both
/// sides were stale together; now that a sentinel bump reloads <c>IConfiguration</c>, a controller reading it per request
/// would sign with a changed key, issuer or audience that validation does not know, and every new token would be rejected
/// until a restart. One snapshot keeps the two in step: a <c>KI:Token</c> change needs a restart.
/// </summary>
public sealed record TokenSettings(
    string? IssuerSigningKey,
    string? TokenIssuer,
    string? TokenAudience,
    string? TokenLifetimeInMins,
    string? AuthUser,
    string? AuthPassword)
{
    /// <summary>The settings in <c>KI:Token</c> as they are now (call it once, at startup).</summary>
    public static TokenSettings From(IConfiguration configuration)
    {
        var section = configuration.GetSection("KI:Token");
        return new(section["IssuerSigningKey"], section["TokenIssuer"], section["TokenAudience"],
            section["TokenLifetimeInMins"], section["AuthUser"], section["AuthPassword"]);
    }

    /// <summary>Never prints the key or the password (a record prints every member by default).</summary>
    public override string ToString() => $"TokenSettings {{ TokenIssuer = {TokenIssuer}, TokenAudience = {TokenAudience} }}";
}
