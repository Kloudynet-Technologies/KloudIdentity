using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore
{
    public interface IReplaceResource<T> : IAPIMapperBase<T>
        where T : Resource
    {
        Task<T> ReplaceAsync(T resource, string appId, string correlationID);
    }
}
