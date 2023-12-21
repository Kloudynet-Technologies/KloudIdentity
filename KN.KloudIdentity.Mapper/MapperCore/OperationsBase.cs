//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Base class for API mappers that provides common properties and methods.
/// </summary>
/// <typeparam name="T">The type of resource that the mapper operates on.</typeparam>
public abstract class OperationsBase<T> : IAPIMapperBase<T> where T : Resource
{
    public required string AppId { get; set; }
    public required T Resource { get; set; }
    public string? CorrelationID { get; set; }
    public required JObject Payload { get; set; }

    private readonly IConfigReader _configReader;
    private readonly IAuthContext _authContext;

    public OperationsBase(IConfigReader configReader, IAuthContext authContext)
    {
        _configReader = configReader;
        _authContext = authContext;
    }

    public abstract Task MapAndPreparePayloadAsync();

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the mapper configuration.</returns>
    public virtual async Task<MapperConfig> GetAppConfigAsync()
    {
        return await _configReader.GetConfigAsync(AppId);
    }

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <returns>The authentication token.</returns>
    public virtual async Task<string> GetAuthenticationAsync(AuthConfig config)
    {
        return await _authContext.GetTokenAsync(config);
    }

    public void CreateUserIdMapper(UserIdMapperUtil userIdMapperUtil, string createdUserId, string identifier, string appId, string correlationId)
    {
        if (string.IsNullOrEmpty(createdUserId))
        {
            throw new ArgumentNullException(nameof(createdUserId));
        }

        if (string.IsNullOrEmpty(identifier))
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        if (string.IsNullOrEmpty(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        userIdMapperUtil.AddUserIdMapper(identifier, createdUserId, appId);
    }
}
