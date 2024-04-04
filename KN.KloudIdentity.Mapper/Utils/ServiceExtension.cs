//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Config.Db;
using KN.KloudIdentity.Mapper.Consumers;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Group;
using KN.KloudIdentity.Mapper.MapperCore.User;
using MassTransit;
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
        services.AddScoped<Context>();

        services.AddScoped<AutoMapperConfig>();
        services.AddScoped<IAuthContext, AuthContextV1>();
        services.AddScoped<IAuthStrategy, ApiKeyStrategy>();
        services.AddScoped<IAuthStrategy, BasicAuthStrategy>();
        services.AddScoped<IAuthStrategy, OAuth2Strategy>();
        services.AddScoped<IConfigReader, ConfigReaderSQL>();
        services.AddScoped<ICreateResource<Core2EnterpriseUser>, CreateUser>();
        services.AddScoped<IGetResource<Core2EnterpriseUser>, GetUser>();
        services.AddScoped<IDeleteResource<Core2EnterpriseUser>, DeleteUser>();
        services.AddScoped<IReplaceResource<Core2EnterpriseUser>, ReplaceUser>();
        services.AddScoped<IUpdateResource<Core2EnterpriseUser>, UpdateUser>();
        services.AddScoped<ICreateResource<Core2Group>, CreateGroup>();
        services.AddScoped<IDeleteResource<Core2Group>, DeleteGroup>();
        services.AddScoped<IReplaceResource<Core2Group>, ReplaceGroup>();
        services.AddScoped<IUpdateResource<Core2Group>, UpdateGroup>();
        services.AddScoped<IAddGroupMembers, AddMembersToGroup>();
        services.AddScoped<IRemoveGroupMembers, RemoveGroupMembers>();
        services.AddScoped<IRemoveAllGroupMembers, RemoveAllGroupMembers>();
        services.AddScoped<IGetResource<Core2Group>, GetGroup>();

        services.AddScoped<IGetFullAppConfigQuery, GetFullAppConfigQuery>();
        services.AddSingleton<RabbitMQUtil>();
        services.AddScoped<IGetVerifiedAttributeMapping, GetVerifiedAttributeMapping>();

        // services.AddMassTransit(x =>
        // {
        //     x.UsingRabbitMq((context, config) =>
        //     {
        //         config.Host(new Uri(configuration["RabbitMQ:Hostname"]!), h =>
        //         {
        //             h.Username(configuration["RabbitMQ:UserName"]!);
        //             h.Password(configuration["RabbitMQ:Password"]!);
        //         });

        //         config.ReceiveEndpoint("verify_mapping_requests", e =>
        //         {
        //             e.Consumer<MappingVerificationConsumer>(context);
        //         });

        //         x.AddRequestClient<AppConfig>(new Uri("exchange:app-config"));
        //     });
        // });
    }
}
