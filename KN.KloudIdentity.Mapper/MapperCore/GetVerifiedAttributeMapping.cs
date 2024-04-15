using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class GetVerifiedAttributeMapping : IGetVerifiedAttributeMapping
{
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public GetVerifiedAttributeMapping(IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }

    public async Task<JObject> GetVerifiedAsync(string appId, ObjectTypes type, SCIMDirections direction, HttpRequestTypes httpRequestType)
    {
        if (direction == SCIMDirections.Inbound)
        {
            return new JObject() { ["Message"] = "Inbound direction is not supported" };
        }

        var appConfig = await GetAppConfigAsync(appId);

        if (appConfig == null)
            throw new NotFoundException("Application not found");

        if (type == ObjectTypes.Group)
        {
            var groupAttributes = appConfig.GroupAttributeSchemas?.Where(x => x.HttpRequestType == httpRequestType && x.SCIMDirection == direction).ToList();

            return JSONParserUtilV2<Resource>.Parse(groupAttributes, new Core2Group(), true);
        }
        else if (type == ObjectTypes.User)
        {
            var userAttributes = appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == httpRequestType && x.SCIMDirection == direction).ToList();

            return JSONParserUtilV2<Resource>.Parse(userAttributes, new Core2EnterpriseUser(), true);
        }


        return null;
    }

    private async Task<AppConfig?> GetAppConfigAsync(string appId)
    {
        var result = await _getFullAppConfigQuery.GetAsync(appId);

        return result;
    }
}
