using System;

namespace KN.KloudIdentity.Mapper.Domain.Messaging.LinuxIntegration;

public record LinuxRequestMessage(string Host, string Username, string Command);