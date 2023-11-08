using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Class for creating a new Core2User resource.
/// Implements the ICreateResource interface.
/// </summary>
public class CreateUser : OperationsBase<Core2User>, ICreateResource<Core2User>
{
    /// <summary>
    /// Executes the creation of a new user asynchronously.
    /// </summary>
    /// <param name="resource">The user resource to create.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>The created user resource.</returns>
    public Task<Core2User> ExecuteAsync(Core2User resource, string appId, string correlationID)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Map and prepare the payload to be sent to the API asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="NotImplementedException"></exception>
    public override Task MapAndPreparePayloadAsync()
    {
        throw new NotImplementedException();
    }
}
