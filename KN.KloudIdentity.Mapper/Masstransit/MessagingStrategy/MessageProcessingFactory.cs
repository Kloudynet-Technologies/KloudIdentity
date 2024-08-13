using KN.KloudIdentity.Mapper.MapperCore;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class MessageProcessingFactory 
{
    private readonly IGetVerifiedAttributeMapping _getVerifiedAttributeMapping;

    public MessageProcessingFactory(IGetVerifiedAttributeMapping getVerifiedAttributeMapping)
    {
        _getVerifiedAttributeMapping = getVerifiedAttributeMapping;
    }

    public IMessageProcessorStrategy CreateProcessor(string action)
    {
        return action switch
        {
            "GetVerifyMapping" => new GetVerifiedAttributeMappingStrategy(_getVerifiedAttributeMapping),
            _ => throw new InvalidOperationException($"Action {action} is not supported")
        };
    }
}
