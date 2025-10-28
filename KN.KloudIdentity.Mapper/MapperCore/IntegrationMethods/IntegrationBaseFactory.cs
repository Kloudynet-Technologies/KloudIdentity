using KN.KloudIdentity.Mapper.Domain.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class IntegrationBaseFactory : IIntegrationBaseFactory
{
    private readonly IServiceProvider _provider;
    private readonly IConfiguration _configuration;
    private readonly IList<IIntegrationBase> _integrations;
    private readonly Dictionary<string, IIntegrationBase> _integrationTypeDict;

    public IntegrationBaseFactory(IServiceProvider provider, IConfiguration configuration, IList<IIntegrationBase> integrations)
    {
        _provider = provider;
        _configuration = configuration;
        _integrations = integrations;
        _integrationTypeDict = integrations.ToDictionary(i => i.GetType().Name, i => i);
    }

    public IIntegrationBase GetIntegration(IntegrationMethods integrationMethod, string appId = "")
    {
        // Filter integrations by the specified integration method
        var integrations = _integrations.Where(i => i.IntegrationMethod == integrationMethod);
        if (!integrations.Any())
        {
            throw new InvalidOperationException($"No integrations registered for integration method: {integrationMethod}");
        }

        // Check for specific appId mapping in configuration
        if (!string.IsNullOrEmpty(appId))
        {
            var mapping = _configuration.GetSection("IntegrationMappings:AppIdToIntegration")
                                               .Get<Dictionary<string, string>>();
            if (mapping != null && mapping.TryGetValue(appId, out var integrationType))
            {
                return integrationType switch
                {
                    var typeName when _integrationTypeDict.ContainsKey(typeName) => _integrationTypeDict[typeName],
                    _ => throw new InvalidOperationException($"Integration type '{integrationType}' is not registered.")
                };
            }
        }

        // Fallback to the last matching integration method if no specific mapping is found.
        // This assumes the last registered integration is the default for that method.
        return integrations.Last();
    }
}
