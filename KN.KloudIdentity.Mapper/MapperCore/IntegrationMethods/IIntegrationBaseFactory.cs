using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IIntegrationBaseFactory
{
    IIntegrationBase GetIntegration(IntegrationMethods integrationMethod, string appId = "");
}
