using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public class AuthenticationFlowStep
{
    public required string StepTitle { get; init; }
    public int StepOrder { get; init; }
    public AuthenticationMethods AuthenticationMethod { get; init; }
    public Guid? CredentialId { get; init; }
    public AuthOnFailureAction OnFailureAction { get; init; }
    public bool IsRequired { get; init; }
    public required dynamic AuthenticationDetails { get; init; }
}
