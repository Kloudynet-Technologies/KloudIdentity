namespace KN.KloudIdentity.Mapper.Domain.Authentication
{
    public enum OAuth2GrantTypes
    {
        ClientCredentials = 1,
        AuthorizationCode = 2,
        RefreshToken = 3,
        DeviceCode = 4,
        PKCE = 5
    }
}
