//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Config.Db;
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
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

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
        services.AddScoped<Context>();

        services.AddScoped<AutoMapperConfig>();
        services.AddScoped<IAuthContext, AuthContextV1>();
        services.AddScoped<IAuthStrategy, ApiKeyStrategy>();
        services.AddScoped<IAuthStrategy, BasicAuthStrategy>();
        services.AddScoped<IAuthStrategy, OAuth2Strategy>();
        services.AddScoped<IConfigReader, ConfigReaderSQL>();

        services.AddScoped<IList<IIntegrationBase>>(provider =>
        {
            return provider.GetServices<IIntegrationBase>().ToList();
        });
        services.AddScoped<ICreateResourceV2, CreateUserV2>();
        services.AddScoped<IIntegrationBase, RESTIntegration>();
        services.AddScoped<IIntegrationBase, LinuxIntegration>();
        services.AddScoped<IIntegrationBase, AS400Integration>();
        services.AddScoped<IIntegrationBase, ODBCIntegration>();
        services.AddScoped<IReqStagQueuePublisher, ReqStagQueuePublisherV1>();
        services.AddScoped<IGetResourceV2, GetUserV2>();
        services.AddScoped<IReplaceResourceV2, ReplaceUserV2>();
        services.AddScoped<IUpdateResourceV2, UpdateUserV2>();
        services.AddScoped<IDeleteResourceV2, DeleteUserV2>();

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
    }
}
