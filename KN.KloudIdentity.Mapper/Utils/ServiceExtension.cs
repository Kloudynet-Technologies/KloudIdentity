//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using Microsoft.Extensions.DependencyInjection;

namespace KN.KloudIdentity.Mapper;

public static class ServiceExtension
{
    public static void ConfigureMapperServices(this IServiceCollection services)
    {
        services.AddScoped<AutoMapperConfig>();
        services.AddScoped<IAuthContext, AuthContextV1>();
        services.AddScoped<IAuthStrategy, ApiKeyStrategy>();
        services.AddScoped<IAuthStrategy, BasicAuthStrategy>();
        services.AddScoped<IAuthStrategy, OAuth2Strategy>();
        services.AddScoped<IConfigReader, ConfigReaderSQL>();
    }
}
