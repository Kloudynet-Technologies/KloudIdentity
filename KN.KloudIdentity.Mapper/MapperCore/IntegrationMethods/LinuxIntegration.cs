using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Domain.Messaging.LinuxIntegration;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using KN.KloudIdentity.Mapper.Domain;
using System.Web.Http;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class LinuxIntegration : IIntegrationBase
{
    public IntegrationMethods IntegrationMethod { get; init; }
    private readonly JSONParserUtilV2<Core2EnterpriseUser> _jsonParserUtilV2;
    private readonly IReqStagQueuePublisher _reqStagQueuePublisher;
    private readonly IOptions<AppSettings> _appSettings;

    private readonly string urnPrefix = "urn:kn:ki:schema:";

    public LinuxIntegration(IReqStagQueuePublisher reqStagQueuePublisher, IOptions<AppSettings> appSettings)
    {
        IntegrationMethod = IntegrationMethods.Linux;
        _jsonParserUtilV2 = new JSONParserUtilV2<Core2EnterpriseUser>();
        _reqStagQueuePublisher = reqStagQueuePublisher;

        _appSettings = appSettings;
    }

    public async Task DeleteAsync(string username, AppConfig appConfig, string correlationID)
    {
        string command = $"sudo usermod -L {username}";

        LinuxRequestMessage linuxRequestMessage = new LinuxRequestMessage(
            appConfig.IntegrationDetails,
            appConfig.AuthenticationDetails,
            command
        );

        var response = await SendMessage(correlationID, linuxRequestMessage, OperationTypes.Delete, default);

        if (response == null || response?.IsError == true)
        {
            Log.Error(
                "Error occurred while disabling the user. AppId: {AppId}, Username: {Username}, CorrelationID: {CorrelationID}, Error: {ErrorMessage}",
                appConfig.AppId, username, correlationID, response?.ErrorMessage);
            throw new ApplicationException($"Error occurred while disabling the user: {response?.ErrorMessage}");
        }
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationID,
        CancellationToken cancellationToken = default)
    {
        string command = $"sudo getent passwd";

        LinuxRequestMessage linuxRequestMessage = new LinuxRequestMessage(
            appConfig.IntegrationDetails,
            appConfig.AuthenticationDetails,
            command
        );

        var response = await SendMessage(correlationID, linuxRequestMessage, OperationTypes.List, cancellationToken);
        var user = new Core2EnterpriseUser();

        if (response == null || response?.IsError == true)
        {
            Log.Error(
                "Error occurred while fetching the user. AppId: {AppId}, Identifier: {Identifier}, CorrelationID: {CorrelationID}, Error: {ErrorMessage}",
                appConfig.AppId, identifier, correlationID, response?.ErrorMessage);
            throw new ApplicationException($"Error occurred while fetching the user: {response?.ErrorMessage}");
        }
        else
        {
            LinuxUserResponse? userList =
                JsonConvert.DeserializeObject<LinuxUserResponse>(response!.Message.ToString());

            var userFields = userList.Total > 0 ? userList.Users.FirstOrDefault(p => p.Identifier == identifier) : null;
            if (userFields != null)
            {
                user.UserName = userFields.Username;
                user.Identifier = userFields.Identifier;

                return user;
            }

            Log.Error(
                "User not found for the identifier {Identifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                identifier, appConfig.AppId, correlationID);

            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }
    }

    private StagingQueueResponseMessage DecryptWithPublicKey(string message)
    {
        // Retrieve the public key from Azure Key Vault
        string publicKey = _appSettings.Value.ExternalQueueEncryptionKey;

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKey.ToCharArray());
        byte[] decryptedBytes = rsa.Decrypt(Convert.FromBase64String(message), RSAEncryptionPadding.Pkcs1);

        var result =
            JsonConvert.DeserializeObject<StagingQueueResponseMessage>(Encoding.UTF8.GetString(decryptedBytes));

        return result!;
    }

    public async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default, params dynamic[] args)
    {
        return await Task.FromResult(config.AuthenticationDetails);
    }

    public async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateAttributeSchema(schema))
        {
            throw new ArgumentException("Invalid attribute schema.");
        }

        var valuesForCommand = new Dictionary<string, string>
        {
            { "Username", GetValueFromResource(schema, resource, "Username") },
            { "UID", GetValueFromResource(schema, resource, "UID") },
            { "Identifier", GetValueFromResource(schema, resource, "Identifier", true) } ,
            { "GroupName", GetValueFromResource(schema, resource, "GroupName", true) }
        };

        return await Task.FromResult(valuesForCommand);
    }

    private string GetValueFromResource(IList<AttributeSchema> schema, Core2EnterpriseUser resource,
        string destinationField, bool allowEmpty = false)
    {
        var field = schema.FirstOrDefault(x => x.DestinationField.Replace(urnPrefix, string.Empty) == destinationField)
                        ?.SourceValue
                    ?? throw new ArgumentNullException(destinationField,
                        $"{destinationField} not configured in attribute mappings.");
        var value = JSONParserUtilV2<Core2EnterpriseUser>.ReadProperty(resource, field)?.ToString();

        if (!allowEmpty && string.IsNullOrEmpty(value))
        {
            Log.Error("{DestinationField} is empty.", destinationField);
            throw new ArgumentNullException(destinationField, $"{destinationField} is empty.");
        }

        return value ?? string.Empty;
    }

    private bool ValidateAttributeSchema(IList<AttributeSchema> schema)
    {
        if (schema == null || schema.Count == 0)
        {
            Log.Error("Attribute schema is null or empty.");
            throw new ArgumentNullException("Attribute schema is empty.");
        }

        if (schema.Any(x => string.IsNullOrEmpty(x.SourceValue) || string.IsNullOrEmpty(x.DestinationField)))
        {
            Log.Error("Source value or destination field is null or empty in the attribute schema.");
            throw new ArgumentNullException("Source value or destination field is empty.");
        }

        return true;
    }

    public async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationID,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationID);

        Dictionary<string, string> valuesForCommand = payload;

        string groupName = valuesForCommand["GroupName"];
        var commandBuilder = new StringBuilder();

        commandBuilder.Append($"sudo useradd -u {valuesForCommand["UID"]} -c \"{valuesForCommand["Identifier"]}\" {valuesForCommand["Username"]} ");
        commandBuilder.Append($"&& sudo passwd -d {valuesForCommand["Username"]} ");
       
        // Assuming group already exists in Linux server.
        // TODO: If group does not exist, extend this logic to create the group before creating the user and assigning to the group.
        if (!string.IsNullOrEmpty(groupName))
        {
            commandBuilder.Append($"&& sudo usermod -aG {groupName} {valuesForCommand["Username"]}");
        }

        string command = commandBuilder.ToString().Trim();

        LinuxRequestMessage linuxRequestMessage = new LinuxRequestMessage(
            appConfig.IntegrationDetails,
            appConfig.AuthenticationDetails,
            command
        );

        var responseMessage =
            await SendMessage(correlationID, linuxRequestMessage, OperationTypes.Create, cancellationToken);

        if (responseMessage == null || responseMessage?.IsError == true)
        {
            Log.Error(
                "Error occurred while creating the user. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {ErrorMessage}",
                appConfig.AppId, correlationID, responseMessage?.ErrorMessage);
            throw new ApplicationException($"Error occurred while creating the user: {responseMessage?.ErrorMessage}");
        }
        
        return null;
    }

    private async Task<StagingQueueResponseMessage> SendMessage(string correlationID,
        LinuxRequestMessage linuxRequestMessage, OperationTypes operationType, CancellationToken cancellationToken)
    {
        StagingQueueRequestMessage request = new StagingQueueRequestMessage(
            correlationID,
            linuxRequestMessage,
            HostTypes.Linux,
            operationType
        );

        var encryptedMessage = EncryptWithPrivateKey(request);

        var responseMessage =
            await _reqStagQueuePublisher.SendAsync(encryptedMessage, correlationID, operationType, cancellationToken);
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

    public Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationID)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationID)
    {
        Log.Information("Provisioning started for user updating. AppId: {AppId}, CorrelationID: {CorrelationID}",
           appConfig.AppId, correlationID);

        Dictionary<string, string> valuesForCommand = payload;

        string username = valuesForCommand["Username"];
        string newGroupName = valuesForCommand["GroupName"];        

        var commandBuilder = new StringBuilder();

        // Update comment field
        commandBuilder.Append($"sudo usermod -c \"{valuesForCommand["Identifier"]}\" {username}");

        // Handle group assignment      
        List<string> currentGroups = await GetUserGroupsAsync(username, correlationID, appConfig); ;

        if (!string.IsNullOrEmpty(newGroupName))
        {
            // If group name is different, update group membership
            if (!currentGroups.Contains(newGroupName))
            {                
                commandBuilder.Append($" && sudo usermod -aG {newGroupName} {username}");
            }
        }

        if (currentGroups.Count > 0)
        { 
            // Remove user from the current group           
            foreach (var group in currentGroups)
            {
                commandBuilder.Append($" && sudo gpasswd -d {username} {group}");
            }
        }

        string command = commandBuilder.ToString();

        LinuxRequestMessage linuxRequestMessage = new LinuxRequestMessage(
            appConfig.IntegrationDetails,
            appConfig.AuthenticationDetails,
            command
        );

        var response = await SendMessage(correlationID, linuxRequestMessage, OperationTypes.Update, default);

        if (response == null || response?.IsError == true)
        {
            Log.Error(
                "Error occurred while updating the user. AppId: {AppId}, Identifier: {Identifier}, CorrelationID: {CorrelationID}, Error: {ErrorMessage}",
                appConfig.AppId, resource.Identifier, correlationID, response?.ErrorMessage);
            throw new ApplicationException($"Error occurred while updating the user: {response?.ErrorMessage}");
        }
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationID,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, Array.Empty<string>()));
    }

    private async Task<List<string>> GetUserGroupsAsync(string username, string correlationId, AppConfig appConfig)
    {
        string command = $"sudo id -nG {username} | tr ' ' '\\n' | grep -vx \"$(id -gn {username})\"";

        var request = new LinuxRequestMessage(
            appConfig.IntegrationDetails,
            appConfig.AuthenticationDetails,
            command
        );

        var response = await SendMessage(correlationId, request, OperationTypes.UserGroup, default);

        if (response == null || response?.IsError == true)
        {
            throw new ApplicationException($"Failed to get user groups: {response?.ErrorMessage}");            
        }

        var userGroupList = JsonConvert.DeserializeObject<LinuxUserGroupResponse>(response!.Message.ToString());

        return userGroupList?.Groups ?? new List<string>();
    }
}