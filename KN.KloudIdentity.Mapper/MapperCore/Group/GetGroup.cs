//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using System.Web.Http;
using Serilog;

namespace KN.KloudIdentity.Mapper;

public class GetGroup : OperationsBase<Core2Group>, IGetResource<Core2Group>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private AppConfig _appConfig;
    private readonly IConfiguration _configuration;
    private readonly IKloudIdentityLogger _logger;

    public GetGroup(IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IKloudIdentityLogger logger)
        : base(authContext, getFullAppConfigQuery)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Core2Group> GetAsync(string identifier, string appId, string correlationID)
    {
        Log.Information("Executing GetGroup for {Identifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
            identifier, appId, correlationID);
        _appConfig = await GetAppConfigAsync(appId);

        var groupURIs = _appConfig?.GroupURIs?.FirstOrDefault();

        if (groupURIs != null && groupURIs!.Get != null)
        {
            var token = await GetAuthenticationAsync(_appConfig, SCIMDirections.Outbound);

            var client = _httpClientFactory.CreateClient();
            Utils.HttpClientExtensions.SetAuthenticationHeaders(client, _appConfig.AuthenticationMethodOutbound,
                _appConfig.AuthenticationDetails, token);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(groupURIs.Get.ToString(), identifier));

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

                _ = CreateLogAsync(_appConfig, identifier, correlationID);

                Log.Information(
                    "GetGroup operation completed successfully. Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                    identifier, appId, correlationID);

                return core2Group;
            }
            else
            {
                Log.Error(
                    "GET API for groups failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}",
                    appId, correlationID, response.StatusCode);
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }
        }
        else
        {
            Log.Error("GET API for groups is not configured. AppId: {AppId}, CorrelationID: {CorrelationID}",
                appId, correlationID);
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
            Log.Error("Field {FieldName} not found in the user schema. AppId: {AppId}", fieldName, appConfig.AppId);
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }
    }

    private string GetValueCaseInsensitive(JObject jsonObject, string propertyName)
    {
        var property = jsonObject.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property?.Value.ToString();
    }

    private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
    {
        var eventInfo = $"Get Group from #{appConfig.AppName}({appConfig.AppId})";
        var logMessage = $"Get group for the id {identifier}";

        var logEntity = new CreateLogEntity(
            appConfig.AppId,
            LogType.Read.ToString(),
            LogSeverities.Information,
            eventInfo,
            logMessage,
            correlationID,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }
}