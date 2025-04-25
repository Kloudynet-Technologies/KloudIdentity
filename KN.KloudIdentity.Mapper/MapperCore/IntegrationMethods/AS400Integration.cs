using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using KN.KloudIdentity.Mapper.Domain.Messaging.AS400Integration;
using Newtonsoft.Json;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.As400;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Represents an AS400 integration.
/// </summary>
public class AS400Integration(
    IReqStagQueuePublisher reqStagQueuePublisher,
    IOptions<AppSettings> appSettings,
    IListAs400GroupsQuery listAs400GroupsQuery)
    : IIntegrationBase
{
    public IntegrationMethods IntegrationMethod { get; init; } = IntegrationMethods.AS400;

    private readonly string _urnPrefix = "urn:kn:ki:schema:";

    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var apiPath = CombineApiPath(appConfig.IntegrationDetails, $"/api/USERS?identifier={identifier}");

        var requestMessage = new AS400RequestMessage(apiPath, basicAuth.Username, basicAuth.Password, string.Empty);
        var responseMessage =
            await SendMessage(correlationId, requestMessage, OperationTypes.Delete, CancellationToken.None);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            Log.Error(
                "Error occurred while deleting the user. AppId: {AppId}, CorrelationId: {CorrelationId}, Identifier: {Identifier}, Error: {ErrorMessage}",
                appConfig.AppId, correlationId, identifier, responseMessage?.ErrorMessage);
            throw new HttpRequestException($"Error occurred while deleting the user: {responseMessage?.ErrorMessage}");
        }
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        var apiPath = CombineApiPath(appConfig.IntegrationDetails, $"/api/USERS?identifier={identifier}");

        var requestMessage = new AS400RequestMessage(apiPath, basicAuth.Username, basicAuth.Password, string.Empty);
        var response = await SendMessage(correlationId, requestMessage, OperationTypes.List, cancellationToken);

        if (response == null || response.IsError == true)
            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);

        List<AS400User> users = JsonConvert.DeserializeObject<List<AS400User>>(response.Message.ToString());
        var userFields = users?.FirstOrDefault(u => u.Identifier == identifier);

        if (userFields == null)
        {
            Log.Error(
                "User not found for the identifier {Identifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                identifier, appConfig.AppId, correlationId);

            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }

        return new Core2EnterpriseUser
        {
            UserName = userFields.Username,
            Identifier = userFields.Identifier
        };
    }

    public async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default)
    {
        if (config.AuthenticationDetails == null)
        {
            Log.Error("Authentication details are missing. AppId: {AppId}", config.AppId);
            throw new HttpRequestException("Authentication details are missing.");
        }

        var basicAuth = JsonConvert.DeserializeObject<BasicAuthentication>(config.AuthenticationDetails.ToString())
                        ?? throw new ArgumentException("Invalid authentication details.");

        if (string.IsNullOrWhiteSpace(basicAuth.Username) || string.IsNullOrWhiteSpace(basicAuth.Password))
        {
            Log.Error("Username or Password is missing. AppId: {AppId}", config.AppId);
            throw new HttpRequestException("Username or Password is missing.");
        }

        return await Task.FromResult(basicAuth);
    }

    public async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateAttributeSchema(schema))
        {
            throw new HttpRequestException("Invalid attribute schema.");
        }

        string groupProfile = string.Empty;
        string supplementalGroupProfile = string.Empty;
        var userClassValue = schema.FirstOrDefault(x => x.DestinationField.Contains("UserClass"))!.SourceValue;
        var groupProfileAttribute = schema.FirstOrDefault(x => x.DestinationField.Contains("GroupProfile"));
        var supplementalGroupProfileAttribute =
            schema.FirstOrDefault(x => x.DestinationField.Contains("SupplementalGroupProfile"));

        if (groupProfileAttribute != null)
        {
            groupProfile = GetValueFromResource(schema, resource, "GroupProfile") ?? string.Empty;
        }

        if (supplementalGroupProfileAttribute != null)
        {
            supplementalGroupProfile =
                GetValueFromResource(schema, resource, "SupplementalGroupProfile") ?? string.Empty;
            supplementalGroupProfile = supplementalGroupProfile.Replace(',', ' ');
        }

        var payload = new Dictionary<string, string>
        {
            { "username", GetValueFromResource(schema, resource, "Username") },
            { "description", GetValueFromResource(schema, resource, "Description") },
            { "userClass", userClassValue },
            { "identifier", GetValueFromResource(schema, resource, "Identifier") },
            { "groupProfile", groupProfile },
            { "supplementalGroupProfile", supplementalGroupProfile }
        };

        return await Task.FromResult(payload);
    }

    private string GetValueFromResource(IList<AttributeSchema> schema, Core2EnterpriseUser resource,
        string destinationField)
    {
        var field = schema.FirstOrDefault(x => x.DestinationField.Replace(_urnPrefix, string.Empty) == destinationField)
                        ?.SourceValue
                    ?? throw new HttpRequestException($"{destinationField} not configured in attribute mappings.");
        var value = JSONParserUtilV2<Core2EnterpriseUser>.ReadProperty(resource, field)?.ToString();

        if (string.IsNullOrEmpty(value) &&
            !destinationField.Equals("GroupProfile", StringComparison.OrdinalIgnoreCase) &&
            !destinationField.Equals("SupplementalGroupProfile", StringComparison.OrdinalIgnoreCase))
        {
            Log.Error("{DestinationField} is empty.", destinationField);
            throw new HttpRequestException($"{destinationField} is empty.");
        }

        return value ?? string.Empty;
    }

    public async Task ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);

        await ValidatePayloadAsync(payload, appConfig, correlationId, cancellationToken);
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, default);
        var apiPath = CombineApiPath(appConfig.IntegrationDetails, "/api/USERS");
        var requestPayload = new
        {
            correlationId = correlationId,
            requestPayload = payload
        };

        string jsonStringPayload = JsonConvert.SerializeObject(requestPayload);
        var as400RequestMessage =
            new AS400RequestMessage(apiPath, basicAuth.Username, basicAuth.Password, jsonStringPayload);

        var responseMessage =
            await SendMessage(correlationId, as400RequestMessage, OperationTypes.Create, cancellationToken);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            Log.Error(
                "Error occurred while creating the user. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {ErrorMessage}",
                appConfig.AppId, correlationId, responseMessage?.ErrorMessage);

            throw new HttpRequestException($"Error occurred while creating the user: {responseMessage?.ErrorMessage}");
        }
    }

    private async Task<StagingQueueResponseMessage> SendMessage(string correlationId,
        AS400RequestMessage as400RequestMessage, OperationTypes operationType, CancellationToken cancellationToken)
    {
        StagingQueueRequestMessage request = new(correlationId, as400RequestMessage, HostTypes.AS400, operationType);
        var encryptedMessage = EncryptWithPrivateKey(request);
        var responseMessage =
            await reqStagQueuePublisher.SendAsync(encryptedMessage, correlationId, operationType, cancellationToken);
        var result = JsonConvert.DeserializeObject<StagingQueueResponseMessage>(responseMessage);

        return result!;
    }

    private string EncryptWithPrivateKey(StagingQueueRequestMessage message)
    {
        string privateKey = appSettings.Value.ExternalQueueEncryptionKey;
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(privateKey.ToCharArray());
        string messageJson = JsonConvert.SerializeObject(message);
        byte[] encryptedBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(messageJson), RSAEncryptionPadding.Pkcs1);

        return Convert.ToBase64String(encryptedBytes);
    }

    public async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        ValidatePayloadAsync(payload, appConfig, correlationId, CancellationToken.None);
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var apiPath = CombineApiPath(appConfig.IntegrationDetails, "/api/USERS");

        var requestPayload = new
        {
            correlationId = correlationId,
            requestPayload = new
            {
                identifier = payload["identifier"],
                description = payload["description"],
                groupProfile = string.IsNullOrEmpty(payload["groupProfile"]) ? "*NONE" : payload["groupProfile"],
                supplementalGroupProfile = string.IsNullOrEmpty(payload["supplementalGroupProfile"])
                    ? "*NONE"
                    : payload["supplementalGroupProfile"]
            }
        };

        string jsonStringPayload = JsonConvert.SerializeObject(requestPayload);
        var as400RequestMessage =
            new AS400RequestMessage(apiPath, basicAuth.Username, basicAuth.Password, jsonStringPayload);
        var responseMessage = await SendMessage(correlationId, as400RequestMessage, OperationTypes.Update,
            CancellationToken.None);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            Log.Error(
                "Error occurred while updating the user. AppId: {AppId}, CorrelationID: {CorrelationId}, Identifier: {Identifier}, Error: {ErrorMessage}",
                appConfig.AppId, correlationId, payload["identifier"], responseMessage?.ErrorMessage);
            throw new HttpRequestException($"Error occurred while updating the user: {responseMessage?.ErrorMessage}");
        }
    }

    public async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        await ValidatePayloadAsync(payload, appConfig, correlationId, CancellationToken.None);
        var basicAuth = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var apiPath = CombineApiPath(appConfig.IntegrationDetails, "/api/USERS");

        var requestPayload = new
        {
            correlationId = correlationId,
            requestPayload = new
            {
                identifier = payload["identifier"],
                description = payload["description"],
                groupProfile = string.IsNullOrEmpty(payload["groupProfile"]) ? "*NONE" : payload["groupProfile"],
                supplementalGroupProfile = string.IsNullOrEmpty(payload["supplementalGroupProfile"])
                    ? "*NONE"
                    : payload["supplementalGroupProfile"]
            }
        };

        string jsonStringPayload = JsonConvert.SerializeObject(requestPayload);

        var as400RequestMessage =
            new AS400RequestMessage(apiPath, basicAuth.Username, basicAuth.Password, jsonStringPayload);
        var responseMessage = await SendMessage(correlationId, as400RequestMessage, OperationTypes.Update,
            CancellationToken.None);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            Log.Error(
                "Error occurred while updating the user. AppId: {AppId}, CorrelationID: {CorrelationId}, Identifier: {Identifier}, Error: {ErrorMessage}",
                appConfig.AppId, correlationId, payload["identifier"], responseMessage?.ErrorMessage);
            throw new HttpRequestException($"Error occurred while updating the user: {responseMessage?.ErrorMessage}");
        }
    }

    public async Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var groups = string.IsNullOrEmpty(payload["groupProfile"])
            ? []
            : (List<As400Group>)await listAs400GroupsQuery.ListAsync(appConfig.AppId, cancellationToken);

        ValidateUsername(payload["username"]);
        ValidateUserClass(payload["userClass"]);
        ValidateGroupProfile(payload["groupProfile"], groups);
        ValidateSupplementalGroupProfile(payload["supplementalGroupProfile"], groups);

        if (string.IsNullOrEmpty(payload["groupProfile"]) && !string.IsNullOrEmpty(payload["supplementalGroupProfile"]))
        {
            Log.Error(
                "GroupProfile is missing while SupplementalGroupProfile is provided. CorrelationId: {CorrelationId}",
                correlationId);
            throw new HttpRequestException("GroupProfile must be provided when SupplementalGroupProfile is provided.");
        }

        return (true, []);
    }

    private static bool ValidateAttributeSchema(IList<AttributeSchema> schema)
    {
        if (schema == null || schema.Count == 0)
        {
            Log.Error("Attribute schema is empty.");

            throw new HttpRequestException("Attribute schema is empty.");
        }

        if (schema.Any(x => string.IsNullOrEmpty(x.SourceValue) || string.IsNullOrEmpty(x.DestinationField)))
        {
            Log.Error("Source value or destination field is empty in the attribute schema.");
            throw new HttpRequestException("Source value or destination field is empty.");
        }

        return true;
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 10 || !username.All(char.IsLetterOrDigit))
        {
            Log.Error(
                $"Invalid Username. Reason: {(string.IsNullOrWhiteSpace(username) ? "Username is empty." : username.Length > 10 ? "Username exceeds 10 characters." : "Username contains non-alphanumeric characters.")}");
            throw new HttpRequestException("Invalid Username.");
        }
    }

    private static void ValidateUserClass(string userClass)
    {
        var validUserClasses = new[] { "*USER", "*PGMR" };
        if (!validUserClasses.Contains(userClass))
        {
            Log.Error("Invalid UserClass: {UserClass}. Allowed values are '*USER' or '*PGMR'.", userClass);
            throw new HttpRequestException("UserClass must be either '*USER' or '*PGMR'.");
        }
    }

    private static void ValidateGroupProfile(string groupProfile, List<As400Group> groups)
    {
        var isExistingGroup = groups.Any(x => x.GroupName == groupProfile);

        if (!string.IsNullOrEmpty(groupProfile))
        {
            if (!isExistingGroup)
            {
                Log.Error("GroupProfile '{GroupProfile}' does not exist in the list of valid groups.", groupProfile);
                throw new HttpRequestException("Invalid GroupProfile: Group does not exist.");
            }

            if (groupProfile.Length > 10)
            {
                Log.Error("GroupProfile '{GroupProfile}' exceeds the maximum length of 10 characters.", groupProfile);
                throw new HttpRequestException("Invalid GroupProfile: Exceeds maximum length.");
            }

            if (!groupProfile.All(char.IsLetterOrDigit))
            {
                Log.Error("GroupProfile '{GroupProfile}' contains non-alphanumeric characters.", groupProfile);
                throw new HttpRequestException("Invalid GroupProfile: Contains invalid characters.");
            }
        }
    }

    private static void ValidateSupplementalGroupProfile(string supplementalGroupProfile, List<As400Group> groups)
    {
        if (string.IsNullOrEmpty(supplementalGroupProfile)) return;

        var totalGroups = supplementalGroupProfile.Split(' ');
        if (supplementalGroupProfile.Length > 200)
        {
            Log.Error("SupplementalGroupProfile exceeds the maximum length of 200 characters.");
            throw new HttpRequestException("SupplementalGroupProfile must be at most 200 characters long.");
        }

        if (totalGroups.Length > 16)
        {
            Log.Error("SupplementalGroupProfile contains more than 16 groups.");
            throw new HttpRequestException("SupplementalGroupProfile must not contain more than 16 groups.");
        }

        var isNotExistingGroup = totalGroups.Any(group => groups.All(x => x.GroupName != group));
        if (isNotExistingGroup)
        {
            Log.Error(
                "SupplementalGroupProfile contains invalid groups that do not exist in the list of valid groups.");
            throw new HttpRequestException(
                "SupplementalGroupProfile contains invalid groups that do not exist in the list of valid groups.");
        }
    }

    private string CombineApiPath(string basePath, string endpoint)
    {
        return basePath.TrimEnd('/') + endpoint;
    }
}