using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Commands;

public class ReqStagQueuePublisherV1 : IReqStagQueuePublisher
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AppSettings> _appSettings;

    public ReqStagQueuePublisherV1(HttpClient httpClient, IOptions<AppSettings> appSettings)
    {
        _httpClient = httpClient;
        _appSettings = appSettings;
    }

    public async Task<string> SendAsync(string request, string correlationID, OperationTypes operationType, CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Add("CorrelationID", correlationID);

        HttpResponseMessage response;
        if (operationType == OperationTypes.Create)
        {
            response = await _httpClient.PostAsync($"{_appSettings.Value.ExternalQueueUrl}/api/users?encryptedMessage={request}", null, cancellationToken);
        }
        else if (operationType == OperationTypes.Update)
        {
            response = await _httpClient.PutAsync($"{_appSettings.Value.ExternalQueueUrl}/api/users?encryptedMessage={request}", null, cancellationToken);
        }
        else if (operationType == OperationTypes.Delete)
        {
            response = await _httpClient.DeleteAsync($"{_appSettings.Value.ExternalQueueUrl}/api/users?encryptedMessage={request}", cancellationToken);
        }
        else if (operationType == OperationTypes.List)
        {
            response = await _httpClient.GetAsync($"{_appSettings.Value.ExternalQueueUrl}/api/users?encryptedMessage={request}", cancellationToken);
        }
        else
        {
            throw new NotSupportedException($"Operation type {operationType} is not supported.");
        }

        response.EnsureSuccessStatusCode();

        var responseContentString = await response.Content.ReadAsStringAsync(cancellationToken);

        return responseContentString;
    }
}
