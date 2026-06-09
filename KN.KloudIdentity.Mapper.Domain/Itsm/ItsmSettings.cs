namespace KN.KloudIdentity.Mapper.Domain.Itsm;

public class ItsmSettings
{
    public int Id { get; set; }
    public string TenantId { get; set; } = null!;
    public ServiceProvider ServiceProvider { get; set; }
    public List<ServiceProviderUrl> ServiceProviderUrls { get; set; } = [];
}