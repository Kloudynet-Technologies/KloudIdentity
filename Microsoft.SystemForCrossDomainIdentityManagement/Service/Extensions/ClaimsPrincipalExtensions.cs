using System.Security.Claims;

namespace Microsoft.SCIM;

public static class ClaimsPrincipalExtensions
{
    private const string TenantIdClaimName = "tid";
    private const string MicrosoftTenantIdClaimName = "http://schemas.microsoft.com/identity/claims/tenantid";

    public static string? GetTenantId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(TenantIdClaimName)?.Value
               ?? principal.FindFirst(MicrosoftTenantIdClaimName)?.Value;
    }
}