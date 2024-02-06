namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public enum AuthenticationMethods
{
    None = 0,
    Basic = 1,
    Bearer = 2,
    OIDC_ClientCrd = 3,
    APIKey = 4,
    SAML = 5
}
