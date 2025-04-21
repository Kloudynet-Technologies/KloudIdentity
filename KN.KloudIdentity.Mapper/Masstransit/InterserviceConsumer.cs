using KN.KI.RabbitMQ.MessageContracts;
using MassTransit;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class InterserviceConsumer(MessageProcessingFactory messageProcessingFactory) : IConsumer<ISCIMServiceRequest>
{
    public async Task Consume(ConsumeContext<ISCIMServiceRequest> context)
    {
        var processor = messageProcessingFactory.CreateProcessor(context.Message.Action);
        var response = await processor.ProcessMessage(context.Message, context.CancellationToken);

        await context.RespondAsync<IInterserviceResponseMsg>(response);
    }
}
