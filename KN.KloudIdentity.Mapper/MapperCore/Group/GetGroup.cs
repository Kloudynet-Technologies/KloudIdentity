//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using System.Web.Http;

namespace KN.KloudIdentity.Mapper;

public class GetGroup : OperationsBase<Core2Group>, IGetResource<Core2Group>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private AppConfig _appConfig;
    private readonly IConfiguration _configuration;

    public GetGroup(IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, IGetFullAppConfigQuery getFullAppConfigQuery)
        : base(authContext, getFullAppConfigQuery)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<Core2Group> GetAsync(string identifier, string appId, string correlationID)
    {
        _appConfig = await GetAppConfigAsync(appId);

        if (_appConfig.GroupURIs!.Get != null && _appConfig.GroupURIs!.Get != null)
        {
            var token = await GetAuthenticationAsync(_appConfig.AuthenticationDetails);

            var client = _httpClientFactory.CreateClient();
            Utils.HttpClientExtensions.SetAuthenticationHeaders(client, _appConfig.AuthenticationMethod, _appConfig.AuthenticationDetails, token);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(_appConfig.GroupURIs.Get.ToString(), identifier));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jObject = JObject.Parse(content);

                var core2Group = new Core2Group();

                string urnPrefix = _configuration["urnPrefix"];

                string idField = GetFieldMapperValue(_appConfig, "Identifier", urnPrefix);
                string displayNameField = GetFieldMapperValue(_appConfig, "DisplayName", urnPrefix);

                core2Group.Identifier = GetValueCaseInsensitive(jObject, idField);
                core2Group.DisplayName = GetValueCaseInsensitive(jObject, displayNameField);

                return core2Group;
            }
            else
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }
        }
        else
        {
            throw new ApplicationException("GET API for groups is not configured.");
        }
    }

    private string GetFieldMapperValue(AppConfig appConfig, string fieldName, string urnPrefix)
    {
        var field = appConfig.GroupAttributeSchemas!.FirstOrDefault(f => f.SourceValue == fieldName);
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
