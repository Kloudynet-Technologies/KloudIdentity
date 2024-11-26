using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using KN.KloudIdentity.Mapper.Domain.Messaging.AS400Integration;
using Newtonsoft.Json;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Represents an AS400 integration.
/// </summary>
public class AS400Integration : IIntegrationBase
{
    public IntegrationMethods IntegrationMethod { get; init; }
    private readonly JSONParserUtilV2<Core2EnterpriseUser> _jsonParserUtilV2;
    private readonly IReqStagQueuePublisher _reqStagQueuePublisher;
    private readonly IOptions<AppSettings> _appSettings;

    private readonly string urnPrefix = "urn:kn:ki:schema:";

    public AS400Integration(IReqStagQueuePublisher reqStagQueuePublisher, IOptions<AppSettings> appSettings)
    {
        IntegrationMethod = IntegrationMethods.AS400;
        _jsonParserUtilV2 = new JSONParserUtilV2<Core2EnterpriseUser>();
        _reqStagQueuePublisher = reqStagQueuePublisher;

        _appSettings = appSettings;
    }

    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationID)
    {
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, default);

        var apiPath = appConfig.IntegrationDetails!.TrimEnd('/') + "/api/users/" + identifier;

        var as400RequestMessage = new AS400RequestMessage(
            apiPath,
            basicAuth.Username,
            basicAuth.Password,
            string.Empty
        );

        var responseMessage = await SendMessage(correlationID, as400RequestMessage, OperationTypes.Delete, default);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            throw new ApplicationException($"Error occurred while deleting the user: {responseMessage?.ErrorMessage}");
        }
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationID, CancellationToken cancellationToken = default)
    {
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken);

        var apiPath = appConfig.IntegrationDetails!.TrimEnd('/') + "/api/users/" + identifier;

        var as400RequestMessage = new AS400RequestMessage(
            apiPath,
            basicAuth.Username,
            basicAuth.Password,
            string.Empty
        );

        var response = await SendMessage(correlationID, as400RequestMessage, OperationTypes.Get, cancellationToken);

        var user = new Core2EnterpriseUser();

        if (response == null || response?.IsError == true)
        {
            throw new ApplicationException($"Error occurred while fetching the user: {response?.ErrorMessage}");
        }
        else
        {
            List<AS400UserResponse>? userList = JsonConvert.DeserializeObject<List<AS400UserResponse>>(response!.Message.ToString());

            var userFields = userList.Count > 0 ? userList.FirstOrDefault(p => p.Identifier == identifier) : null;
              
            if (userFields != null)
            {
                user.UserName = userFields.Username;
                user.Identifier = userFields.Identifier;

                return user;
            }

            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }
    }

    public async Task<dynamic> GetAuthenticationAsync(AppConfig config, SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default)
    {
        if (config?.AuthenticationDetails == null)
        {
            throw new ArgumentException("Authentication details are missing.");
        }

        var basicAuth = JsonConvert.DeserializeObject<BasicAuthentication>(config.AuthenticationDetails.ToString())
                        ?? throw new ArgumentException("Invalid authentication details.");

        if (string.IsNullOrWhiteSpace(basicAuth.Username))
        {
            throw new ArgumentException("Username is missing.");
        }

        if (string.IsNullOrWhiteSpace(basicAuth.Password))
        {
            throw new ArgumentException("Password is missing.");
        }

        return await Task.FromResult(basicAuth);
    }

    public async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
    {
        if (!ValidateAttributeSchema(schema))
        {
            throw new ArgumentException("Invalid attribute schema.");
        }

        var userClassValue = schema.FirstOrDefault(x => x.DestinationField.Contains("UserClass"))!.SourceValue;
        var payload = new Dictionary<string, string>
        {
            { "Username", GetValueFromResource(schema, resource, "Username") },
            { "Description", GetValueFromResource(schema, resource, "Description") },
            { "UserClass", userClassValue },
            { "Identifier", GetValueFromResource(schema, resource, "Identifier") }
        };

        return await Task.FromResult(payload);
    }

    private string GetValueFromResource(IList<AttributeSchema> schema, Core2EnterpriseUser resource, string destinationField)
    {
        var field = schema.FirstOrDefault(x => x.DestinationField.Replace(urnPrefix, string.Empty) == destinationField)?.SourceValue
                    ?? throw new ArgumentNullException(destinationField, $"{destinationField} not configured in attribute mappings.");
        var value = JSONParserUtilV2<Core2EnterpriseUser>.ReadProperty(resource, field)?.ToString();

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(destinationField, $"{destinationField} is empty.");
        }

        return value ?? string.Empty;
    }

    public async Task ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationID, CancellationToken cancellationToken = default)
    {
        await ValidatePayloadAsync(payload, correlationID, cancellationToken);

        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, default);

        var apiPath = appConfig.IntegrationDetails!.TrimEnd('/') + "/api/users";

        string jsonStringPayload = JsonConvert.SerializeObject(payload);

        var as400RequestMessage = new AS400RequestMessage(
            apiPath,
            basicAuth.Username,
            basicAuth.Password,
            jsonStringPayload
        );

        var responseMessage = await SendMessage(correlationID, as400RequestMessage, OperationTypes.Create, cancellationToken);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            throw new ApplicationException($"Error occurred while creating the user: {responseMessage?.ErrorMessage}");
        }
    }

    private async Task<StagingQueueResponseMessage> SendMessage(string correlationID, AS400RequestMessage as400RequestMessage, OperationTypes operationType, CancellationToken cancellationToken)
    {
        StagingQueueRequestMessage request = new StagingQueueRequestMessage(
            correlationID,
            as400RequestMessage,
            HostTypes.AS400,
            operationType
        );

        var encryptedMessage = EncryptWithPrivateKey(request);

        var responseMessage = await _reqStagQueuePublisher.SendAsync(encryptedMessage, correlationID, operationType, cancellationToken);
        // @ToDo: Decrypt the response message
        // var result = DecryptWithPublicKey(responseMessage);

        var result = JsonConvert.DeserializeObject<StagingQueueResponseMessage>(responseMessage);

        return result!;
    }
    private string EncryptWithPrivateKey(StagingQueueRequestMessage message)
    {
        // Retrieve the private key from Azure Key Vault
        string privateKey = _appSettings.Value.ExternalQueueEncryptionKey;

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(privateKey.ToCharArray());

        string messageJson = JsonConvert.SerializeObject(message);
        byte[] encryptedBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(messageJson), RSAEncryptionPadding.Pkcs1);

        return Convert.ToBase64String(encryptedBytes);
    }

    public Task ReplaceAsync(JObject payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationID)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateAsync(JObject payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationID)
    {
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, default);
        var apiPath = appConfig.IntegrationDetails!.TrimEnd('/') + "/api/users/" + resource.Identifier;

        string jsonStringPayload = JsonConvert.SerializeObject(payload);

        var as400RequestMessage = new AS400RequestMessage(
            apiPath,
            basicAuth.Username,
            basicAuth.Password,
            jsonStringPayload
        );

        var responseMessage = await SendMessage(correlationID, as400RequestMessage, OperationTypes.Update, default);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            throw new ApplicationException($"Error occurred while updating the user: {responseMessage?.ErrorMessage}");
        }
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, string correlationID, CancellationToken cancellationToken = default)
    { 
        var username = payload["Username"];
        var userClass = payload["UserClass"];

        ValidateUsername(username);
        ValidateUserClass(userClass);
        
        return Task.FromResult((true, Array.Empty<string>()));
    }

    private bool ValidateAttributeSchema(IList<AttributeSchema> schema)
    {
        if (schema == null || schema.Count == 0)
        {
            throw new ArgumentNullException("Attribute schema is empty.");
        }

        if (schema.Any(x => string.IsNullOrEmpty(x.SourceValue) || string.IsNullOrEmpty(x.DestinationField)))
        {
            throw new ArgumentNullException("Source value or destination field is empty.");
        }

        return true;
    }

    private void ValidateUsername(string username)
    {
        if (username.Length > 10)
        {
            throw new ArgumentException("Username must be at most 10 characters long.");
        }

        if (!username.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException("Username must not contain special characters.");
        }
    }

    private void ValidateUserClass(string userClass)
    {
        var validUserClasses = new[] { "USER", "PGMR" };
        if (!validUserClasses.Contains(userClass))
        {
            throw new ArgumentException("UserClass must be either 'USER' or 'PGMR'.");
        }
    }
}
