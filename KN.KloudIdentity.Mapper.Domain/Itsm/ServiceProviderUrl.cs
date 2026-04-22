using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.Domain.Itsm;

public class ServiceProviderUrl
{
    public int Id { get; set; }
    public ActionNames ActionName { get; set; }
    public string? RequestUrl { get; set; }
}