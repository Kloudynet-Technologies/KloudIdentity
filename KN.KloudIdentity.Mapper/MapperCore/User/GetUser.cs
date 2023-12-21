//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net.Http.Headers;
using System.Web.Http;
using Azure.Identity;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Config;
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
    private readonly IConfigReader _configReader;
    private readonly IHttpClientFactory _httpClientFactory;
    private MapperConfig _appConfig;
    private readonly IConfiguration _configuration;

    private readonly UserIdMapperUtil _userIdMapperUtil;

    public GetUser(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, UserIdMapperUtil userIdMapperUtil)
        : base(configReader, authContext)
    {
        _configReader = configReader;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _userIdMapperUtil = userIdMapperUtil;
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
    public virtual async Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID)
    {
        AppId = appId;
        CorrelationID = correlationID;

        _appConfig = await GetAppConfigAsync();

        if (_appConfig.GETAPIForUsers != null && _appConfig.GETAPIForUsers != string.Empty)
        {
            var token = await GetAuthenticationAsync(_appConfig.AuthConfig);

            // @TODO: Get the created user id from the database based on app config setting.
            var userId = _userIdMapperUtil.GetCreatedUserId(identifier, appId);

            var client = _httpClientFactory.CreateClient();
            client.SetAuthenticationHeaders(_appConfig.AuthConfig, token);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(_appConfig.GETAPIForUsers, userId));

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

    private string GetFieldMapperValue(MapperConfig mapperConfig, string fieldName, string urnPrefix)
    {
        var field = mapperConfig.UserSchema.FirstOrDefault(f => f.MappedAttribute == fieldName);
        if (field != null)
        {
            return field.FieldName.Remove(0, urnPrefix.Length);
        }
        else
        {
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }
    }

    /// <summary>
    /// Maps and prepare the payload to be sent to the API.
    /// </summary>
    public override Task MapAndPreparePayloadAsync()
    {
        throw new NotImplementedException();
    }

    private string GetValueCaseInsensitive(JObject jsonObject, string propertyName)
    {
        var property = jsonObject.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property?.Value.ToString();
    }
}
