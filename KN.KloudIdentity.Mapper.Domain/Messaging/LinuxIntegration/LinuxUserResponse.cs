using System;

namespace KN.KloudIdentity.Mapper.Domain.Messaging.LinuxIntegration;

public class LinuxUserResponse
{
    public int Total { get; set; }

    public List<LinuxUser> Users { get; set; } = new List<LinuxUser>();
}

public class LinuxUser
{
    public required string Username { get; set; }
    public required string UserId { get; set; }
    public required string Identifier { get; set; }
}
