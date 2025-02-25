using KN.KloudIdentity.Mapper.Domain.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Domain.SQL;

public record SQLIntegrationDetails
{
    public Guid Id { get; init; }
    public required string AppId { get; init; }
    public IntegrationMethods IntegrationMethod { get; init; }
    public required string PostSpName { get; init; }
    public required string GetSpName { get; init; }
    public string? PatchSpName { get; init; }
    public string? DeleteSpName { get; init; }

}
