using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IIntegrationBase
{
    /// <summary>
    /// Gets the integration method.
    /// Set in the derived class for each integration method.
    /// </summary>
    IntegrationMethods IntegrationMethod { get; init; }

    /// <summary>
    /// Attribute mapping and prepares the payload asynchronously.
    /// </summary>
    /// <param name="schema">Attribute maooing schema data</param>
    /// <param name="resource">Entra ID object</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <param name="config">App configuration</param>
    /// <param name="direction">SCIM direction</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<dynamic> GetAuthenticationAsync(AppConfig config, SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions the user asynchronously to LOB application.
    /// </summary>
    /// <param name="payload">Payload to be provisioned to LOB app</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the payload asynchronously before been sent to LOB app.
    /// </summary>
    /// <param name="payload">Payload to be sent to LOB app</param>
    /// <param name="appConfig"></param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all users from LOB application asynchronously.
    /// </summary>
    /// <param name="appConfig"></param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <param name="identifier"></param>
    /// <returns>List of users</returns>
    Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="payload">Payload of the user</param>
    /// <param name="resource">Object to be replaced</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <returns></returns>
    Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId);

    /// <summary>
    /// Updates a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="resource"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId);

    /// <summary>
    /// Deletes a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId);
}
