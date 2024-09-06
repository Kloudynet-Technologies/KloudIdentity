using KN.KloudIdentity.Mapper.Domain.Application;
//---------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace KN.KloudIdentity.Mapper.Domain;

public record InboundIntegrationMethod
{
    [Required]
    public IntegrationMethods IntegrationMethod { get; set; }

    public InboundRESTIntegrationMethod? RESTIntegrationMethod { get; set; }

    public IEnumerable<ValidationResult> Validate()
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(this);
        Validator.TryValidateObject(this, validationContext, validationResults, true);

        if (IntegrationMethod == IntegrationMethods.REST && RESTIntegrationMethod == null)
        {
            validationResults.Add(new ValidationResult("RESTIntegrationMethod is required when IntegrationMethod is REST."));
        }

        if (RESTIntegrationMethod != null)
        {
            validationResults.AddRange(RESTIntegrationMethod.Validate());
        }

        return validationResults;
    }
}
