namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public enum AuthenticationMethods
{
    None = 0,
    Basic = 1,
    Bearer = 2,
    OAuth2 = 3,
    APIKey = 4,
    SAML = 5,
    OAuth2ClientCrd = 6,
    DotRez = 7,
    SoapWsSecurity = 8,

    /// <summary>
    /// SOAP HTTP transport authentication (NTLM or Basic, chosen by UseNtlm).
    /// The Management Portal calls this value <c>SoapTransport</c>.
    /// </summary>
    SoapNtlm = 9,

    /// <summary>
    /// Places a token produced by an earlier step into the Authorization header,
    /// custom HTTP headers or a SOAP header. Produces no token itself.
    /// </summary>
    SoapTokenPlacement = 10
}
