namespace KN.KloudIdentity.Mapper.Domain.Setting;

public class ApplicationSetting
{
    public int Id { get; set; }
    public string TenantId { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string ConnectorUrl { get; set; }
}
