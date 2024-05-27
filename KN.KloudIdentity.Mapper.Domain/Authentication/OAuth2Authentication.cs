namespace KN.KloudIdentity.Mapper.Domain.Authentication
{
    public record OAuth2Authentication
    {
        /// <summary>
        /// ClientId
        /// </summary>
        public string ClientId { get; set; } = null!;

        /// <summary>
        /// ClientSecret
        /// </summary>
        public string ClientSecret { get; set; } = null!;

        /// <summary>
        /// Grant type
        /// </summary>
        public OAuth2GrantTypes GrantType { get; set; }

        /// <summary>
        /// Authorization code
        /// </summary>
        public string? AuthorizationCode { get; set; }

        /// <summary>
        /// Redirect uri
        /// </summary>
        public string? RedirectUri { get; set; }

        /// <summary>
        /// Code verifier
        /// </summary>
        public string? CodeVerifier { get; set; }

        /// <summary>
        /// Refresh token
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Scopes
        /// </summary>
        public IEnumerable<string> Scopes { get; set; } = null!;

        /// <summary>
        /// Token aquisition url
        /// </summary>
        public string TokenUrl { get; set; } = null!;

        /// <summary>
        /// Authority
        /// </summary>
        public string? Authority { get; set; }

        /// <summary>
        /// Token Prefix
        /// </summary>
        public string? TokenPrefix { get; set;}
    }
}
