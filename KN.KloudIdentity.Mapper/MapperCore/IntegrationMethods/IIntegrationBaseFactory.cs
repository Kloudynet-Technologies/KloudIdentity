using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IIntegrationBaseFactory
{
    IIntegrationBaseV2 GetIntegration(IntegrationMethods integrationMethod, string appId = "");
}
