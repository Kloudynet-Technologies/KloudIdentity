//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Interface for API mappers that map a resource to a payload and prepare it for sending to an API.
/// </summary>
/// <typeparam name="T">The type of resource being mapped.</typeparam>
[Obsolete("This interface is obsolete. Use IProvisioningBase instead.")]
public interface IAPIMapperBase<T> where T : Resource
{
    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns></returns>
    [Obsolete("This method is obsolete. Use IProvisioningBase.GetAppConfigAsync instead.")]
    Task<AppConfig> GetAppConfigAsync(string appId);

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <returns></returns>
    [Obsolete("This method is obsolete. Use IIntegrationsBase.GetAuthenticationAsync instead.")]
    Task<string> GetAuthenticationAsync(AppConfig config, SCIMDirections direction);

    /// <summary>
    /// Map and prepare the payload to be sent to the API asynchronously.
    /// </summary>
    /// <returns></returns>
    [Obsolete("This method is obsolete. Use IIntegrationsBase.MapAndPreparePayloadAsync instead.")]
    Task<JObject> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, T resource);
}
