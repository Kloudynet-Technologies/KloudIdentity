//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using System.Text.Json;

namespace KN.KloudIdentity.Mapper.Masstransit
{
    public class GetVerifiedAttributeMappingStrategy(IGetVerifiedAttributeMapping getVerifiedAttributeMapping) : IMessageProcessorStrategy
    {
        public async Task<IInterserviceResponseMsg> ProcessMessage(IInterserviceRequestMsg message, CancellationToken cancellationToken)
        {
            var validationResponse = ValidateMessage(message);
            if (validationResponse != null)
            {
                return validationResponse;
            }

            var query = JsonSerializer.Deserialize<VerifyMappingRequest>(message.Message);

            if (query == null)
            {
                return new GetVerifiedAttributeMappingResponse
                {
                    Message = string.Empty,
                    IsError = true,
                    ErrorMessage = "Deserialization failed: query is null."
                };
            }

            var queryResponse = await getVerifiedAttributeMapping.GetVerifiedAsync(
                query.AppId,
                query.Type,
                query.Direction,
                query.HttpRequestType
            );

            return new GetVerifiedAttributeMappingResponse
            {
                Message = queryResponse.ToString(Newtonsoft.Json.Formatting.None),
            };
        }

        private GetVerifiedAttributeMappingResponse? ValidateMessage(IInterserviceRequestMsg message)
        {
            if (message == null || string.IsNullOrEmpty(message?.Message))
            {
                return new GetVerifiedAttributeMappingResponse
                {
                    Message = string.Empty,
                    IsError = true,
                    ErrorMessage = "Request can not be null or empty."
                };
            }

            return null;
        }
    }
}
