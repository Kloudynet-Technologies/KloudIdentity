using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.License;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class LicenseValidationQuery(IRequestClient<IMgtPortalServiceRequestMsg> requestClient) : ILicenseValidationQuery
{
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
            var response = await requestClient.GetResponse<IInterserviceResponseMsg>(message, cancellationToken);

            return ProcessResponse(response.Message);
        }
        catch (MassTransit.RequestTimeoutException ex)
        {
            Log.Error(ex, "License status check request timed out. Error Message: {ErrorMessage}", ex.Message);
            throw new TimeoutException("License status check request timed out.", ex);
        }
        catch (MassTransit.RequestFaultException ex)
        {
            Log.Error(ex, "License status check request faulted. Error Message: {ErrorMessage}", ex.Message);
            throw new InvalidOperationException("License status check request faulted.", ex);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                "An error occurred while processing the license status check request. Error Message: {ErrorMessage}",
                ex.Message);
            throw new InvalidOperationException("An error occurred while processing the license status check request.",
                ex);
        }
    }

    private LicenseStatus ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            Log.Error("License status check failed. Error: {ErrorMessage}",
                response?.ErrorMessage ?? "Unknown error");

            // Return a LicenseStatus indicating failure
            return new LicenseStatus
            {
                IsValid = false,
                Message = response?.ErrorMessage ?? "Unknown error occurred."
            };
        }

        bool isValid = false;
        try
        {
            var deserialized = JsonConvert.DeserializeObject<bool>(response.Message);
            if (deserialized == null)
            {
                Log.Error("License status check failed. Deserialized value is null. Raw message: {RawMessage}", response.Message);
                return new LicenseStatus
                {
                    IsValid = false,
                    Message = "License status could not be determined due to invalid response format."
                };
            }
            isValid = deserialized;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "License status check failed during deserialization. Raw message: {RawMessage}", response.Message);
            return new LicenseStatus
            {
                IsValid = false,
                Message = "License status could not be determined due to deserialization error."
            };
        }

        return new LicenseStatus
        {
            IsValid = isValid,
            Message = isValid ? "License is valid." : "License is invalid."
        };
    }
}