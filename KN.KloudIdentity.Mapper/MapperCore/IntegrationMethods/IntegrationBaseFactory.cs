using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class IntegrationBaseFactory : IIntegrationBaseFactory
{
    private readonly AppSettings _appSettings;
    private readonly IList<IIntegrationBase> _integrations;
    private readonly Dictionary<string, IIntegrationBase> _integrationTypeDict;

    public IntegrationBaseFactory(IList<IIntegrationBase> integrations,
        IOptions<AppSettings> appSettings)
    {
        _integrations = integrations;
        _integrationTypeDict = integrations.ToDictionary(i => i.GetType().Name, i => i);
        _appSettings = appSettings.Value;
    }

    public IIntegrationBase GetIntegration(IntegrationMethods integrationMethod, string appId = "")
    {
        // Filter integrations by the specified integration method
        var integrations = _integrations.Where(i => i.IntegrationMethod == integrationMethod);
        var integrationBases = integrations as IIntegrationBase[] ?? integrations.ToArray();
        if (integrationBases.Length == 0)
        {
            throw new InvalidOperationException(
                $"No integrations registered for integration method: {integrationMethod}");
        }

        var integrationMapping = _appSettings.IntegrationMappings;
        
        // Check for specific appId mapping in configuration
        if (!string.IsNullOrEmpty(appId))
        {
            if (integrationMapping.AppIdToIntegration.TryGetValue(appId, out var integrationType))
            {
                if (_integrationTypeDict.TryGetValue(integrationType, out var integration))
                {
                    return integration;
                }
            }
        }

        // Use DefaultIntegration mapping from configuration
        if (integrationMapping.DefaultIntegration.TryGetValue(integrationMethod.ToString(), out var defaultIntegrationType))
        {
            if (_integrationTypeDict.TryGetValue(defaultIntegrationType, out var defaultIntegration))
            {
                return defaultIntegration;
            }
        }
        
        throw new InvalidOperationException($"No integrations registered for integration method: {integrationMethod}");
    }
}