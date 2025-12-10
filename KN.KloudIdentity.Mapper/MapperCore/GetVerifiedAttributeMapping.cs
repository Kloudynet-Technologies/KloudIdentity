using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class GetVerifiedAttributeMapping : IGetVerifiedAttributeMapping
{
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public GetVerifiedAttributeMapping(IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }

    public async Task<JObject> GetVerifiedAsync(string appId, ObjectTypes type, int stepId)
    {
        Log.Information($"Getting verified attribute for {appId}");

        var appConfig = await GetAppConfigAsync(appId);

        if (appConfig == null)
        {
            Log.Warning(
                $"Application configuration not found for the provided App ID: {appId}. Ensure the App ID is correct and properly configured.");
            throw new NotFoundException(
                $"Application configuration for App ID '{appId}' was not found. Please verify the App ID and try again.");
        }       

        if (type == ObjectTypes.Group)
        {
            var groupAttributes = appConfig.GroupAttributeSchemas?.Where(x => x.ActionStepId == stepId)
                .ToList();

            if (groupAttributes == null)
            {
                Log.Error($"Group attributes not found for App ID: {appId} and Step Id: {stepId}");
                throw new NotFoundException("Group attributes not found");
            }

            return JSONParserUtilV2<Resource>.Parse(groupAttributes, new Core2Group(), true);
        }
        else if (type == ObjectTypes.User)
        {
            var userAttributes = appConfig.UserAttributeSchemas.Where(x => x.ActionStepId == stepId)
                .ToList();

            if (userAttributes == null)
            {
                Log.Error($"User attributes not found for App ID: {appId} and Step Id: {stepId}");
                throw new NotFoundException("User attributes not found");
            }

            return JSONParserUtilV2<Resource>.Parse(userAttributes, new Core2EnterpriseUser(), true);
        }

        Log.Error("Unsupported object type encountered: {ObjectType}. AppId: {AppId}", type, appId);
        throw new NotSupportedException("Object type not supported");
    }

    private async Task<AppConfig?> GetAppConfigAsync(string appId)
    {
        var result = await _getFullAppConfigQuery.GetAsync(appId);

        return result;
    }
}