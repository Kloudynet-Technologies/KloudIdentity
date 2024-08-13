using KN.KI.RabbitMQ.MessageContracts;
using MassTransit;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class InterserviceConsumer : IConsumer<ISCIMServiceRequest>
{
    private readonly MessageProcessingFactory _messageProcessingFactory;

    public InterserviceConsumer(MessageProcessingFactory messageProcessingFactory)
    {
        _messageProcessingFactory = messageProcessingFactory;
    }

    public async Task Consume(ConsumeContext<ISCIMServiceRequest> context)
    {
        var processor = _messageProcessingFactory.CreateProcessor(context.Message.Action);
        var response = await processor.ProcessMessage(context.Message, context.CancellationToken);

        await context.RespondAsync<IInterserviceResponseMsg>(response);
    }
}
