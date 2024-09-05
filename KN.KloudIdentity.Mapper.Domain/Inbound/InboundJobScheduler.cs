//---------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper.Domain;

public record InboundJobScheduler
{
    [Required]
    public string InboundJobFrequency { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate()
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);
        Validator.TryValidateObject(this, validationContext, validationResults, true);

        return validationResults;
    }
}
