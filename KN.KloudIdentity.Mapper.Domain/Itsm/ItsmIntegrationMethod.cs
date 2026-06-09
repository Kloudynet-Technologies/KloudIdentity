namespace KN.KloudIdentity.Mapper.Domain.Itsm;

public class ItsmIntegrationMethod
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public int ITSMSettingId { get; set; }
    public Dictionary<string, string> AdditionalProperties { get; set; } = [];
}