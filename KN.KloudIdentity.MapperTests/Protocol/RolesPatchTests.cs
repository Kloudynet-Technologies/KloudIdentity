using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests.Protocol;

/// <summary>
/// How a SCIM PATCH on <c>roles</c> lands on a <see cref="Core2EnterpriseUser"/> through
/// <c>user.Apply(PatchRequest2)</c>, which is what UpdateUserV4 runs. Entra's app-role mapping
/// (<c>appRoleAssignments</c> to <c>roles[primary eq "True"].value</c>) is the shape that matters for Flagright.
/// </summary>
public class RolesPatchTests
{
    // Operations are built from the JSON exactly as Entra sends them, so the value keeps its wire shape
    // (a plain string stays a string, an array stays an array).
    private static Core2EnterpriseUser Apply(string operationJson)
    {
        var op = JsonConvert.DeserializeObject<PatchOperation2Combined>(operationJson)!;

        var request = new PatchRequest2();
        request.AddOperation(op);

        var user = new Core2EnterpriseUser();
        user.Apply(request);
        return user;
    }

    private static string?[] RoleValues(Core2EnterpriseUser user) =>
        user.Roles?.Select(r => r.Value).ToArray() ?? Array.Empty<string?>();

    // Baseline: proves the harness applies a PATCH at all.
    [Fact]
    public void Baseline_ReplaceDisplayName_IsApplied()
    {
        var user = Apply("""{"op":"Replace","path":"displayName","value":"Jane Doe"}""");

        Assert.Equal("Jane Doe", user.DisplayName);
    }

    // ---- the shapes Entra sends for the app-role mapping ----

    [Fact]
    public void Entra_Replace_RolesPrimaryValue_WithPlainString_SetsRole()
    {
        var user = Apply("""{"op":"Replace","path":"roles[primary eq \"True\"].value","value":"Analyst"}""");

        Assert.Equal(new[] { "Analyst" }, RoleValues(user));
    }

    [Fact]
    public void Entra_Add_RolesPrimaryValue_WithPlainString_SetsRole()
    {
        var user = Apply("""{"op":"Add","path":"roles[primary eq \"True\"].value","value":"Analyst"}""");

        Assert.Equal(new[] { "Analyst" }, RoleValues(user));
    }

    [Fact]
    public void Replace_Roles_WithArrayOfValueObjects_SetsRole()
    {
        var user = Apply("""{"op":"Replace","path":"roles","value":[{"value":"Analyst","primary":true}]}""");

        Assert.Equal(new[] { "Analyst" }, RoleValues(user));
    }

    // ---- each value as JSON object text (the shape the code understood before plain strings) ----

    [Fact]
    public void Replace_Roles_WhenEachValueIsJsonObjectText_SetsRole()
    {
        var user = Apply("""{"op":"Replace","path":"roles","value":[{"value":"{\"value\":\"Analyst\",\"displayName\":\"Analyst\"}"}]}""");

        Assert.Equal(new[] { "Analyst" }, RoleValues(user));
    }

    // ---- end to end: patched user to the mapped value Flagright's PATCH body is built from ----

    [Fact]
    public void RoleMapping_ReadsPatchedRole_FromRolesZeroValue()
    {
        var user = Apply("""{"op":"Replace","path":"roles[primary eq \"True\"].value","value":"Analyst"}""");
        var rows = new List<AttributeSchema>
        {
            new()
            {
                DestinationField = "urn:kn:ki:schema:role",
                DestinationType = AttributeDataTypes.String,
                MappingType = MappingTypes.Direct,
                SourceValue = "Roles[0]:Value"
            }
        };

        var body = JSONParserUtilV2<Core2EnterpriseUser>.Parse(rows, user);

        Assert.Equal("Analyst", (string?)body["role"]);
    }

    // ---- the single-valued alternative: carry the role in a plain custom attribute instead of "roles" ----

    [Fact]
    public void KiExtensionAttribute_Replace_WithPlainString_IsApplied()
    {
        var user = Apply("""{"op":"Replace","path":"urn:ietf:params:scim:schemas:extension:ki:1.0:User:extensionAttribute1","value":"Analyst"}""");

        Assert.Equal("Analyst", user.KIExtension.ExtensionAttribute1);
    }

    [Fact]
    public void RoleMapping_ReadsPatchedKiExtensionAttribute()
    {
        var user = Apply("""{"op":"Replace","path":"urn:ietf:params:scim:schemas:extension:ki:1.0:User:extensionAttribute1","value":"Analyst"}""");
        var rows = new List<AttributeSchema>
        {
            new()
            {
                DestinationField = "urn:kn:ki:schema:role",
                DestinationType = AttributeDataTypes.String,
                MappingType = MappingTypes.Direct,
                SourceValue = "KIExtension:ExtensionAttribute1"
            }
        };

        var body = JSONParserUtilV2<Core2EnterpriseUser>.Parse(rows, user);

        Assert.Equal("Analyst", (string?)body["role"]);
    }
}
