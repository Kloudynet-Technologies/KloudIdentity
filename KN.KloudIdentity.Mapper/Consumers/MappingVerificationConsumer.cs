using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.MapperCore;
using MassTransit;

namespace KN.KloudIdentity.Mapper.Consumers
{
    public class MappingVerificationConsumer : IConsumer<MappingVerification>
    {
        private readonly IGetVerifiedAttributeMapping _getVerifiedAttributeMapping;
        private readonly IPublishEndpoint _publishEndpoint;

        public MappingVerificationConsumer(IGetVerifiedAttributeMapping getVerifiedAttributeMapping, IPublishEndpoint publishEndpoint)
        {
            _getVerifiedAttributeMapping = getVerifiedAttributeMapping;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<MappingVerification> context)
        {
            var result = await _getVerifiedAttributeMapping.GetVerifiedAsync(context.Message.AppId, context.Message.ObjectType, context.Message.HttpMethod, context.Message.StepId);

            await _publishEndpoint.Publish(result);
        }
    }
}
