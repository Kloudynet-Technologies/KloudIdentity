//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.Utils;

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
        services.AddScoped<ICreateResource<Core2User>, CreateUser>();
        services.AddScoped<IGetResource<Core2User>, GetUser>();
       // services.AddScoped<IAPIMapperBase<Core2EnterpriseUser>, OperationsBase<Core2EnterpriseUser>>();
    }
}
