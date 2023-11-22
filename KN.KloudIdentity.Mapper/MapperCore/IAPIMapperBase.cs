//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Interface for API mappers that map a resource to a payload and prepare it for sending to an API.
/// </summary>
/// <typeparam name="T">The type of resource being mapped.</typeparam>
public interface IAPIMapperBase<T> where T : Resource
{
    /// <summary>
    /// Gets or sets the ID of the application.
    /// </summary>
    string AppId { get; set; }

    /// <summary>
    /// SCIM object coming from the Azure AD.
    /// </summary>
    T Resource { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for Azure AD.
    /// </summary>
    string? CorrelationID { get; set; }

    /// <summary>
    /// Gets or sets the payload to be sent to the API.
    /// </summary>
    JObject Payload { get; set; }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<MapperConfig> GetAppConfigAsync();

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<string> GetAuthenticationAsync(AuthConfig config);

    /// <summary>
    /// Map and prepare the payload to be sent to the API asynchronously.
    /// </summary>
    /// <returns></returns>
    Task MapAndPreparePayloadAsync();
}
