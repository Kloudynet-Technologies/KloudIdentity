using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Implementation of IGetResource interface for retrieving a Core2User resource.
/// </summary>
public class GetUser : OperationsBase<Core2EnterpriseUser>, IGetResource<Core2EnterpriseUser>
{
    private readonly IConfigReader _configReader;

    public GetUser(IConfigReader configReader, IAuthContext authContext)
        : base(configReader, authContext)
    {
        _configReader = configReader;
    }

    /// <summary>
    /// Retrieves a user by identifier and application ID asynchronously.
    /// </summary>
    /// <param name="identifier">The identifier of the user to retrieve.</param>
    /// <param name="appId">The ID of the application the user belongs to.</param>
    /// <param name="correlationID">The correlation ID for the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved user.</returns>
    public Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Maps and prepare the payload to be sent to the API.
    /// </summary>
    public override Task MapAndPreparePayloadAsync()
    {
        throw new NotImplementedException();
    }
}
