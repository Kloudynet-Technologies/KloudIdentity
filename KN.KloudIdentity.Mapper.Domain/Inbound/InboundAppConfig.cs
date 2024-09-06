//---------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper.Domain;

public record InboundAppConfig
{
    [Required]
    public string AppId { get; set; } = null!;

    public bool IsInboundJobEnabled { get; set; }

    [Required]
    public InboundAuthConfig InboundAuthConfig { get; set; } = null!;

    [Required]
    public InboundIntegrationMethod InboundIntegrationMethod { get; set; } = null!;

    public InboundJobScheduler? InboundJobScheduler { get; set; }

    public IEnumerable<ValidationResult> Validate()
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);
        Validator.TryValidateObject(this, validationContext, validationResults, true);

        if (InboundAuthConfig != null)
        {
            validationResults.AddRange(InboundAuthConfig.Validate());
        }
        if (InboundIntegrationMethod != null)
        {
            validationResults.AddRange(InboundIntegrationMethod.Validate());
        }

        if (IsInboundJobEnabled)
        {
            if (InboundJobScheduler == null)
            {
                validationResults.Add(new ValidationResult("InboundJobScheduler is required when IsInboundJobEnabled is true."));
            }
            else
            {
                validationResults.AddRange(InboundJobScheduler.Validate());
            }
        }


        return validationResults;
    }
}
