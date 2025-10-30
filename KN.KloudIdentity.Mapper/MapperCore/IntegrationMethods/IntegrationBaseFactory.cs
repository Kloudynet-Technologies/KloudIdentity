using KN.KloudIdentity.Mapper.Domain.Application;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class IntegrationBaseFactory : IIntegrationBaseFactory
{
    private readonly IConfiguration _configuration;
    private readonly IList<IIntegrationBase> _integrations;
    private readonly Dictionary<string, IIntegrationBase> _integrationTypeDict;

    public IntegrationBaseFactory(IConfiguration configuration, IList<IIntegrationBase> integrations)
    {
        _configuration = configuration;
        _integrations = integrations;
        _integrationTypeDict = integrations.ToDictionary(i => i.GetType().Name, i => i);
    }

    public IIntegrationBase GetIntegration(IntegrationMethods integrationMethod, string appId = "")
    {
        // Filter integrations by the specified integration method
        var integrations = _integrations.Where(i => i.IntegrationMethod == integrationMethod);
        var integrationBases = integrations as IIntegrationBase[] ?? integrations.ToArray();
        if (!integrationBases.Any())
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
                if (_integrationTypeDict.TryGetValue(integrationType, out var integration))
                {
                    return integration;
                }
            }
        }

        // Use DefaultIntegration mapping from configuration
        var defaultMapping = _configuration.GetSection("IntegrationMappings:DefaultIntegration")
                                           .Get<Dictionary<string, string>>();
        if (defaultMapping != null)
        {
            var methodKey = integrationMethod.ToString();
            if (defaultMapping.TryGetValue(methodKey, out var defaultIntegrationType))
            {
                if (_integrationTypeDict.TryGetValue(defaultIntegrationType, out var defaultIntegration))
                {
                    return defaultIntegration;
                }
            }
        }
        
        throw new InvalidOperationException($"No integrations registered for integration method: {integrationMethod}");
    }
}
