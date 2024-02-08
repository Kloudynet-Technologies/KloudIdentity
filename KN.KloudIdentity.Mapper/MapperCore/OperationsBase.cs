//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Base class for API mappers that provides common properties and methods.
/// </summary>
/// <typeparam name="T">The type of resource that the mapper operates on.</typeparam>
public abstract class OperationsBase<T> : IAPIMapperBase<T> where T : Resource
{
    private readonly IAuthContext _authContext;
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public OperationsBase(
        IAuthContext authContext,
        IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _authContext = authContext;
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }

    public virtual Task<JObject> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, T resource)
    {
        var payload = JSONParserUtil<Resource>.Parse(schema, resource);
        return Task.FromResult(payload);
    }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the mapper configuration.</returns>
    public virtual async Task<AppConfig> GetAppConfigAsync(string appId)
    {
        var result = await _getFullAppConfigQuery.GetAsync(appId);
        if (result == null)
        {
            throw new KeyNotFoundException($"App configuration not found for app ID {appId}.");
        }

        return result;
    }

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <returns>The authentication token.</returns>
    public virtual async Task<string> GetAuthenticationAsync(AppConfig config)
    {
        return await _authContext.GetTokenAsync(config);
    }
}
