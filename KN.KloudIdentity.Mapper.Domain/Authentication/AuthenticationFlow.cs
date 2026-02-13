using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public class AuthenticationFlow
{
    public int Id { get; set; }
    public string AppId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public SCIMDirections Purpose { get; set; }
    public bool IsActive { get; set; }
    public List<AuthenticationFlowStep> Steps { get; set; } = new();
}
