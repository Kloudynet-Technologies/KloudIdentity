using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.License;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class LicenseStatusCheckQuery : ILicenseStatusCheckQuery
{
    private readonly IRequestClient<IMgtPortalServiceRequestMsg> _requestClient;

    public LicenseStatusCheckQuery(
        IRequestClient<IMgtPortalServiceRequestMsg> requestClient
    )
    {
        _requestClient = requestClient;
    }

    public async Task<LicenseStatus> IsLicenseValidAsync(CancellationToken cancellationToken = default)
    {
        return await SendMessageAndProcessResponse(cancellationToken);
    }

    private async Task<LicenseStatus> SendMessageAndProcessResponse(CancellationToken cancellationToken)
    {
        var message = new MgtPortalServiceRequestMsg(
            string.Empty,
            nameof(ActionType.LicenseStatusCheck),
            Guid.NewGuid().ToString(),
            null
        );

        try
        {
            var response = await _requestClient.GetResponse<IInterserviceResponseMsg>(message, cancellationToken);

            return ProcessResponse(response.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "An error occurred while processing the license status check request. Error Message: {ErrorMessage}",
                ex.Message);
            throw new InvalidOperationException(ex.Message);
            throw new InvalidOperationException("An error occurred while processing the license status check request.", ex);
    }

    private static LicenseStatus ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            Log.Error("License status check failed. Error: {ErrorMessage}", response?.ErrorMessage ?? "Unknown error");
            
            // Return a LicenseStatus indicating failure
           return new LicenseStatus
            {
                IsValid = false,
                Message = response?.ErrorMessage ?? "Unknown error occurred."
            };
        }

        var isValid = JsonConvert.DeserializeObject<bool>(response.Message);
        
        return new LicenseStatus
        {
            IsValid = isValid,
            Message = isValid ? "License is valid." : "License is invalid."
        };
    }
}