//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.Common.AppConfig;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Commands;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Group;
using KN.KloudIdentity.Mapper.MapperCore.Inbound;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.User;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.MapperCore.User;
using KN.KloudIdentity.Mapper.Masstransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.Utils;

/// <summary>
/// Provides extension methods for configuring services to be injected related to mapping.
/// </summary>
public static class ServiceExtension
{
    /// <summary>
    /// Configures the mapper services for the application.
    /// </summary>
    /// <param name="services">The collection of services to add the mapper services to.</param>
    public static void ConfigureMapperServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();
        services.AddHttpClient(AppConstant.NtlmSoapClientName)
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new HttpClientHandler
                    {
                        UseDefaultCredentials = true
                        // or static credentials if your app uses fixed service credentials
                    });

        services.AddMemoryCache();
        services.AddScoped<IAuthContext, AuthContextV2>();
        services.AddScoped<IAuthStrategy, ApiKeyStrategy>();
        services.AddScoped<IAuthStrategy, BasicAuthStrategy>();
        services.AddScoped<IAuthStrategy, BearerAuthStratergy>();
        services.AddScoped<IAuthStrategy, OAuth2Strategy>();
        services.AddScoped<IAuthStrategy, DotRezAuthStrategy>();
        services.AddScoped<ISoapAuthApplier, SoapTransportAuthApplier>();
        services.AddScoped<ISoapAuthApplier, WsSecuritySoapAuthApplier>();
        services.AddScoped<ISoapAuthApplier, SoapTokenHeaderApplier>();

        services.AddScoped<IList<IIntegrationBaseV2>>(provider => provider.GetServices<IIntegrationBaseV2>().ToList());

        var appSettingsSection = configuration.GetSection("KI");
        var appSettings = appSettingsSection.Get<AppSettings>();
        var connectionString = appSettings?.UserMigration?.AzureStorageConnectionString;
        var authMethod = appSettings?.UserMigration?.AuthMethod;

        if (!string.IsNullOrWhiteSpace(authMethod))
        {
            services.AddSingleton<IAzureStorageManager>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new AzureStorageManager(connectionString, authMethod, options, configuration);
            });
        }

        services.AddScoped<ICreateResourceV2, CreateUserV4>();

        services.AddScoped<IIntegrationBase, RestIntegrationManageEngine>();
        services.AddScoped<IIntegrationBaseV2, RESTIntegrationV4>();
        services.AddScoped<IIntegrationBaseV2, ITSMIntegration>();

        services.AddScoped<IIntegrationBase, RESTIntegration>();
        services.AddScoped<IIntegrationBase, LinuxIntegration>();
        services.AddScoped<IIntegrationBase, AS400Integration>();
        services.AddScoped<IIntegrationBase, SQLIntegration>();
        services.AddScoped<IIntegrationBaseV2, SOAPIntegration>();
        services.AddScoped<IIntegrationBaseV2, EagleSOAPIntegration>();

        services.AddScoped<IIntegrationBaseFactory, IntegrationBaseFactory>();

        services.AddScoped<IReqStagQueuePublisher, ReqStagQueuePublisherV1>();
        services.AddScoped<IGetResourceV2, GetUserV4>();
        services.AddScoped<IReplaceResourceV2, ReplaceUserV4>();

        services.AddScoped<IUpdateResourceV2, UpdateUserV4>();
        services.AddScoped<IDeleteResourceV2, DeleteUserV4>();

        services.AddScoped<ICreateResource<Core2Group>, CreateGroup>();
        services.AddScoped<IDeleteResource<Core2Group>, DeleteGroup>();
        services.AddScoped<IReplaceResource<Core2Group>, ReplaceGroup>();
        services.AddScoped<IUpdateResource<Core2Group>, UpdateGroup>();
        services.AddScoped<IAddGroupMembers, AddMembersToGroup>();
        services.AddScoped<IRemoveGroupMembers, RemoveGroupMembers>();
        services.AddScoped<IRemoveAllGroupMembers, RemoveAllGroupMembers>();
        services.AddScoped<IGetResource<Core2Group>, GetGroup>();

        services.AddScoped<IGetFullAppConfigQuery, GetFullAppConfigQuery>();
        services.AddScoped<IGetVerifiedAttributeMapping, GetVerifiedAttributeMapping>();
        services.AddScoped<IFetchInboundResources, ListUserInbound>();
        services.AddScoped<IGraphClientUtil, GraphClientUtil>();
        services.AddScoped<ICreateResourceInbound, CreateUserInbound>();
        services.AddScoped<IGetApplicationSettingQuery, GetApplicationSettingQuery>();
        services.AddScoped<IListApplicationsQuery, ListApplicationsQuery>();
        services.AddScoped<MessageProcessingFactory>();
        services.AddScoped<IJobManagementService, JobManagementService>();
        services.AddScoped<IInboundJobExecutor, InboundJobExecutorService>();
        services.AddScoped<IGetInboundAppConfigQuery, GetInboundAppConfigQuery>();
        services.AddScoped<IInboundMapper, InboundMapper>();
        services.AddScoped<IOutboundPayloadProcessor, OutboundPayloadProcessor>();

        services.AddScoped<IListAs400GroupsQuery, ListAs400GroupsQuery>();

        services.AddScoped<ILicenseValidationQuery, LicenseValidationQuery>();

        services.AddScoped<IAddOrEditAppConfig, AddOrEditAppConfig>();
        services.AddScoped<IDeleteAppConfig, DeleteAppConfig>();
    }
}
