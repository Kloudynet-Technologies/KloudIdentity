using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore
{
    [Obsolete("This interface is deprecated, use IReplaceResourceV2 instead.")]
    public interface IReplaceResource<T> : IAPIMapperBase<T>
        where T : Resource
    {
        [Obsolete("This method is deprecated, use IReplaceResourceV2.ReplaceAsync instead.")]
        Task<T> ReplaceAsync(T resource, string appId, string correlationID);
    }
}
