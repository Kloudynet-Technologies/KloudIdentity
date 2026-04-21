// File: Infrastructure/ExternalAPICalls/Commands/MetaverseIntegrationClient.cs

using System.Text.Json;
using KN.KI.RabbitMQ.MessageContracts;
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

    public Task<T> CreateAsync<T>(
        string tenantId,
        string appId,
        object payload,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(tenantId, appId, new {tenantId, appId, payload }, correlationId, ActionType.DisconnectedUserProvisioning, cancellationToken);

    public Task<T> GetAsync<T>(
        string tenantId,
        string appId,
        string identifier,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(tenantId, appId, new {tenantId, appId, identifier }, correlationId, ActionType.DisconnectedUserRetrieval, cancellationToken);

    public Task<T> UpdateAsync<T>(
        string tenantId,
        string appId,
        string identifier,
        object payload,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(tenantId, appId, new { tenantId, appId, identifier, payload }, correlationId, ActionType.DisconnectedUserUpdate, cancellationToken);

    public Task<T> ReplaceAsync<T>(
        string tenantId,
        string appId,
        string identifier,
        object payload,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(tenantId, appId, new {tenantId, appId, identifier, payload }, correlationId, ActionType.DisconnectedUserReplace, cancellationToken);

    public Task<T> DeleteAsync<T>(
        string tenantId,
        string appId,
        string identifier,
        string correlationId,
        CancellationToken cancellationToken
    ) => SendAsync<T>(tenantId, appId, new {tenantId, appId, identifier }, correlationId, ActionType.DisconnectedUserDeletion, cancellationToken);

    /// <summary>
    /// Sends a request message to the metaverse integration service and processes the response.
    /// This method is used by all the public methods to perform the actual communication with the metaverse service.
    /// </summary>
    private async Task<T> SendAsync<T>(
        string tenantId,
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

            return (T)response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Metaverse request failed | tenantId:{tenantId} AppId: {AppId}",  tenantId, appId);
            throw;
        }
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

        return JsonSerializer.Deserialize<T>(response.Message, JsonOptions)!;
    }
}
