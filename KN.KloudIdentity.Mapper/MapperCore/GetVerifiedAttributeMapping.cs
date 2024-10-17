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

    public async Task<JObject> GetVerifiedAsync(string appId, ObjectTypes type, HttpRequestTypes httpRequestType)
    {
        var appConfig = await GetAppConfigAsync(appId);

        if (appConfig == null)
            throw new NotFoundException("Application not found");

        if (type == ObjectTypes.Group)
        {
            var groupAttributes = appConfig.GroupAttributeSchemas?.Where(x => x.HttpRequestType == httpRequestType).ToList();

            if (groupAttributes == null)
                throw new NotFoundException("Group attributes not found");

            return JSONParserUtilV2<Resource>.Parse(groupAttributes, new Core2Group(), true);
        }
        else if (type == ObjectTypes.User)
        {
            var userAttributes = appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == httpRequestType).ToList();

            if (userAttributes == null)
                throw new NotFoundException("User attributes not found");

            return JSONParserUtilV2<Resource>.Parse(userAttributes, new Core2EnterpriseUser(), true);
        }

        throw new NotSupportedException("Object type not supported");
    }

    private async Task<AppConfig?> GetAppConfigAsync(string appId)
    {
        var result = await _getFullAppConfigQuery.GetAsync(appId);

        return result;
    }
}
