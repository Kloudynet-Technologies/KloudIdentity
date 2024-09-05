//---------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Authentication;
using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper.Domain;

public record InboundAuthConfig
{
    [Required]
    public AuthenticationMethods AuthenticationMethod { get; init; }

    public APIKeyAuthentication? APIKeyAuthentication { get; init; }

    public BearerAuthentication? BearerAuthentication { get; init; }

    public BasicAuthentication? BasicAuthentication { get; init; }

    public OAuth2Authentication? OAuth2Authentication { get; init; }

    public IEnumerable<ValidationResult> Validate()
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);
        Validator.TryValidateObject(this, validationContext, validationResults, true);

        if (AuthenticationMethod == AuthenticationMethods.APIKey && APIKeyAuthentication == null)
        {
            validationResults.Add(new ValidationResult("APIKeyAuthentication is required when AuthenticationMethod is APIKey."));
        }
        if (AuthenticationMethod == AuthenticationMethods.Bearer && BearerAuthentication == null)
        {
            validationResults.Add(new ValidationResult("BearerAuthentication is required when AuthenticationMethod is Bearer."));
        }
        if (AuthenticationMethod == AuthenticationMethods.Basic && BasicAuthentication == null)
        {
            validationResults.Add(new ValidationResult("BasicAuthentication is required when AuthenticationMethod is Basic."));
        }
        if (AuthenticationMethod == AuthenticationMethods.OIDC_ClientCrd && OAuth2Authentication == null)
        {
            validationResults.Add(new ValidationResult("OAuth2Authentication is required when AuthenticationMethod is OIDC_ClientCrd."));
        }

        return validationResults;
    }
}
