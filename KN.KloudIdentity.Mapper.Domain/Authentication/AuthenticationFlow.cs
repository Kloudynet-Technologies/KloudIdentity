using KN.KloudIdentity.Mapper.Domain.Mapping;

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
