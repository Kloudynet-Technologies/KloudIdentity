//---------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper.Domain;

public record InboundRESTIntegrationMethod
{
    public Guid Id { get; init; }

    [Required]
    public string AppId { get; init; } = null!;

    [Required]
    public string UsersEndpoint { get; init; } = null!;

    [Required]
    public string ProvisioningEndpoint { get; init; } = null!;

    public string? JoiningDateProperty { get; init; }

    public TriggerType? CreationTrigger { get; init; }

    public int? CreationTriggerOffsetDays { get; init; }

    public IEnumerable<ValidationResult> Validate()
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);
        Validator.TryValidateObject(this, validationContext, validationResults, true);

        return validationResults;
    }
}
