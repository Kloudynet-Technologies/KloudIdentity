namespace Microsoft.SCIM;

public sealed class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = string.Empty;
}
