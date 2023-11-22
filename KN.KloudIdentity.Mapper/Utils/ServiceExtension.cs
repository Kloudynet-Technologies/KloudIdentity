﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Config.Db;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.Extensions.DependencyInjection;
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
    public static void ConfigureMapperServices(this IServiceCollection services)
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
    }
}
