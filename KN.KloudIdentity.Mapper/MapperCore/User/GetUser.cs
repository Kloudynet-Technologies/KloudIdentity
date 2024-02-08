//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net.Http.Headers;
using System.Web.Http;
using Azure.Identity;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Implementation of IGetResource interface for retrieving a Core2User resource.
/// </summary>
public class GetUser : OperationsBase<Core2EnterpriseUser>, IGetResource<Core2EnterpriseUser>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private AppConfig _appConfig;
    private readonly IConfiguration _configuration;

    public GetUser(IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, IGetFullAppConfigQuery getFullAppConfigQuery, IConfigReader configReader)
        : base(authContext, getFullAppConfigQuery)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Retrieves a user by identifier and application ID asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the user to retrieve.</param>
    /// <param name="appId">The ID of the application the user belongs to.</param>
    /// <param name="correlationID">The correlation ID for the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved user.</returns>
    /// <exception cref="Exception">Error retrieving user.</exception>
    /// <exception cref="ApplicationException">GET API for users is not configured.</exception>
    public async Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID)
    {
        _appConfig = await GetAppConfigAsync(appId);

        if (_appConfig.UserURIs.Get != null && _appConfig.UserURIs.Get != null)
        {
            var token = await GetAuthenticationAsync(_appConfig.AuthenticationDetails);

            var client = _httpClientFactory.CreateClient();
            client = Utils.HttpClientExtensions.SetAuthenticationHeaders(client, _appConfig.AuthenticationDetails, token);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(_appConfig.UserURIs.Get.ToString(), identifier));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<JObject>(content);

                var core2EntUsr = new Core2EnterpriseUser();

                string urnPrefix = _configuration["urnPrefix"];

                string idField = GetFieldMapperValue(_appConfig, "Identifier", urnPrefix);
                string usernameField = GetFieldMapperValue(_appConfig, "UserName", urnPrefix);

                core2EntUsr.Identifier = GetValueCaseInsensitive(user, idField);
                core2EntUsr.UserName = GetValueCaseInsensitive(user, usernameField);

                return core2EntUsr;
            }
            else
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }
        }
        else
        {
            throw new ApplicationException("GET API for users is not configured.");
        }
    }

    private string GetFieldMapperValue(AppConfig appConfig, string fieldName, string urnPrefix)
    {
        var field = appConfig.UserAttributeSchemas.FirstOrDefault(f => f.SourceValue == fieldName);
        if (field != null)
        {
            return field.DestinationField.Remove(0, urnPrefix.Length);
        }
        else
        {
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }
    }

    private string GetValueCaseInsensitive(JObject jsonObject, string propertyName)
    {
        var property = jsonObject.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property?.Value.ToString();
    }
}
