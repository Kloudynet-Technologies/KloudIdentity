//---------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper.Domain;

public record InboundJobSchedulerConfig
{
    [Required]
    public string AppId { get; set; } = null!;

    [Required]
    public bool IsInboundJobEnabled { get; set; }

    public string? InboundJobFrequency { get; set; }

    public IEnumerable<ValidationResult> Validate()
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);
        Validator.TryValidateObject(this, validationContext, validationResults, true);

        if (IsInboundJobEnabled && string.IsNullOrWhiteSpace(InboundJobFrequency))
        {
            validationResults.Add(new ValidationResult("InboundJobFrequency is required when IsInboundJobEnabled is true."));
        }

        return validationResults;
    }
}
