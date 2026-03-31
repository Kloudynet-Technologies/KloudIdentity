// File: Infrastructure/ExternalAPICalls/Commands/MetaverseIntegrationClient.cs

using System.Text.Json;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Serilog;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Commands;

public class MetaverseIntegrationClient(
    IRequestClient<IMetaverseServiceRequestMsg> requestClient
) : IMetaverseIntegrationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<T> CreateAsync<T>(
        string appId,
        object payload,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(appId, new { appId, payload }, correlationId, ActionType.DisconnectedUserProvisioning, cancellationToken);

    public Task<T> GetAsync<T>(
        string appId,
        string identifier,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(appId, new { appId, identifier }, correlationId, ActionType.DisconnectedUserRetrieval, cancellationToken);

    public Task<T> UpdateAsync<T>(
        string appId,
        string identifier,
        object payload,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(appId, new { appId, identifier, payload }, correlationId, ActionType.DisconnectedUserUpdate, cancellationToken);

    public Task<T> ReplaceAsync<T>(
        string appId,
        string identifier,
        object payload,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(appId, new { appId, identifier, payload }, correlationId, ActionType.DisconnectedUserReplace, cancellationToken);

    public Task<T> DeleteAsync<T>(
        string appId,
        string identifier,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(appId, new { appId, identifier }, correlationId, ActionType.DisconnectedUserDeletion, cancellationToken);

    private async Task<T> SendAsync<T>(
        string appId,
        object request,
        string correlationId,
        ActionType action,
        CancellationToken cancellationToken
    )
    {
        var message = new MetaverseServiceRequestMsg(
            JsonSerializer.Serialize(request, JsonOptions),
            action.ToString(), // critical fix
            correlationId,
            null
        );

        try
        {
            var response = await requestClient.GetResponse<IInterserviceResponseMsg>(
                message,
                cancellationToken
            );

            return ProcessResponse<T>(response.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Metaverse request failed | AppId: {AppId}", appId);
            throw;
        }
    }

    private static T ProcessResponse<T>(IInterserviceResponseMsg? response)
    {
        if (response is null || response.IsError == true)
        {
            Log.Error("Response error: {Error}", response?.ErrorMessage);
            throw new InvalidOperationException(response?.ErrorMessage ?? "Unknown error");
        }

        if (string.IsNullOrWhiteSpace(response.Message))
            throw new InvalidOperationException("Response message is empty");

        return JsonSerializer.Deserialize<T>(response.Message, JsonOptions)!;
    }
}