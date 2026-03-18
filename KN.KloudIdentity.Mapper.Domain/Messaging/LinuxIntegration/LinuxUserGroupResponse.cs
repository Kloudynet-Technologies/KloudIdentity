using System;

namespace KN.KloudIdentity.Mapper.Domain.Messaging.LinuxIntegration;

public class LinuxUserGroupResponse
{
    public int Total { get; set; }
    public List<string> Groups { get; set; } = new List<string>();
}
