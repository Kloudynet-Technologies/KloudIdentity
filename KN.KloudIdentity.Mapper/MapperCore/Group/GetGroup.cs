//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper;

public class GetGroup : OperationsBase<Core2Group>, IGetResource<Core2Group>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private MapperConfig _appConfig;
    private readonly IConfiguration _configuration;

    public GetGroup(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration) : base(configReader, authContext)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<Core2Group> GetAsync(string identifier, string appId, string correlationID)
    {
        AppId = appId;
        CorrelationID = correlationID;

        _appConfig = await GetAppConfigAsync();

        if(_appConfig.GETAPIForGroups != null && _appConfig.GETAPIForGroups != string.Empty)
        {
            var token = await GetAuthenticationAsync(_appConfig.AuthConfig);

            var client = _httpClientFactory.CreateClient();
            client.SetAuthenticationHeaders(_appConfig.AuthConfig, token);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(_appConfig.GETAPIForGroups, identifier));

            if(response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jObject = JObject.Parse(content);

                var core2Group = new Core2Group();

                string urnPrefix = _configuration["urnPrefix"];

                string idField = GetFieldMapperValue(_appConfig, "Identifier", urnPrefix);
                string displayNameField = GetFieldMapperValue(_appConfig, "DisplayName", urnPrefix);

                core2Group.Identifier = jObject[idField].ToString();
                core2Group.DisplayName = jObject[displayNameField].ToString();

                return core2Group;
            }
            else
            {
                throw new Exception($"Error retrieving group. Status code: {response.StatusCode}");
            }
        }
        else
        {
            throw new ApplicationException("GET API for groups is not configured.");
        }
    }

    private string GetFieldMapperValue(MapperConfig mapperConfig, string fieldName, string urnPrefix)
    {
        var field = mapperConfig.GroupSchema.FirstOrDefault(f => f.MappedAttribute == fieldName);
        if(field != null)
        {
            return field.FieldName.Remove(0, urnPrefix.Length);
        }
        else
        {
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }
    }

    public override Task MapAndPreparePayloadAsync()
    {
        throw new NotImplementedException();
    }
}
