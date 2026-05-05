// File: Infrastructure/ExternalAPICalls/Commands/MetaverseIntegrationClient.cs

using System.Text.Json;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Itsm;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.Exceptions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Serilog;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Commands;

public class MetaverseIntegrationClient(
    IRequestClient<IMetaverseServiceRequestMsg> requestClient
) : IMetaverseIntegrationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Sends a request message to the metaverse integration service and processes the response.
    /// This method is used by all the public methods to perform the actual communication with the metaverse service.
    /// </summary>
    public async Task<T> SendAsync<T>(
        string request,
        string correlationId,
        ActionType action,
        CancellationToken cancellationToken
    )
    {
        var message = new MetaverseServiceRequestMsg(
            request,
            action.ToString(),
            correlationId,
            null
        );

        var response = await requestClient.GetResponse<IInterserviceResponseMsg>(
            message,
            timeout: RequestTimeout.After(s: 60),
            cancellationToken: cancellationToken
        );

        return ProcessResponse<T>(response.Message);
    }

    private static T ProcessResponse<T>(IInterserviceResponseMsg? response)
    {
        if (response is null || response.IsError == true)
        {
            Log.Error("MetaverseIntegrationClient: Response error: {Error}", response?.ErrorMessage);
            throw new MetaverseIntegrationException(response?.ErrorMessage ?? "Unknown error");
        }

        if (string.IsNullOrWhiteSpace(response.Message))
            throw new MetaverseIntegrationException("MetaverseIntegrationClient: Response message is empty");

        return JsonSerializer.Deserialize<T>(response.Message, JsonOptions)
            ?? throw new MetaverseIntegrationException("MetaverseIntegrationClient: Failed to deserialize response message");
    }
}
