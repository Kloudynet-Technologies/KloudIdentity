//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.RabbitMQ.MessageContracts;

namespace KN.KloudIdentity.Mapper.Masstransit;

public interface IMessageProcessorStrategy
{
    Task<IInterserviceResponseMsg> ProcessMessage(IInterserviceRequestMsg message, CancellationToken cancellationToken);

}
