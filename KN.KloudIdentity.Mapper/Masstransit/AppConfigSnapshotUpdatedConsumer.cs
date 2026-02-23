using KN.KI.RabbitMQ.MessageContracts;
using MassTransit;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class AppConfigSnapshotUpdatedConsumer :IConsumer<IAppConfigSnapshotUpdated>
{
    public async Task Consume(ConsumeContext<IAppConfigSnapshotUpdated> context)
    {
        var msg = context.Message;

        await Task.CompletedTask;
    }
}