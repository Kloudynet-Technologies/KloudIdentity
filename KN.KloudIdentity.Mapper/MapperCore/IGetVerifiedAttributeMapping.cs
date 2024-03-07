using KN.KloudIdentity.Common.Enumr;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IGetVerifiedAttributeMapping
{
    Task<dynamic> GetVerifiedAsync(string appId, MappingType type);
}
