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
    SoapNtlm = 9
}
