using System;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound;

public interface IProvisioningBase
{
    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<AppConfig> GetAppConfigAsync(string appId);

    /// <summary>
    /// Executes custom logic asynchronously.
    /// </summary>
    Task<dynamic> ExecuteCustomLogicAsync(dynamic payload, AppConfig appConfig, string correlationID);
}
