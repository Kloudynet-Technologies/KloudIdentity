using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Repositories;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KN.KloudIdentity.Mapper.Infrastructure.DI;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<KNContext>((sp, options) =>
        {
            var connection =  SqlConnectionFactory
                .CreateAsync(configuration)
                .GetAwaiter()
                .GetResult();

            options.UseSqlServer(
                connection,
                b => b.MigrationsAssembly("KN.KloudIdentity.Mapper.Infrastructure"));
        }, ServiceLifetime.Transient, ServiceLifetime.Transient);
        
        services.AddScoped<IAppConfigSnapshotRepository, AppConfigSnapshotRepository>();
        services.AddScoped<IListApplicationConfigsQuery, ListApplicationConfigsQuery>();
        
        return services;
    }
}