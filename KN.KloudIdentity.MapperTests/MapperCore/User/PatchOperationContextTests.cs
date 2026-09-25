using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests.MapperCore.User;

public class PatchOperationContextTests
{
    private const string EnterpriseSchema = "urn:ietf:params:scim:schemas:extension:enterprise:2.0:User";

    private static PatchOperationContext Capture(params (OperationName Name, string Path)[] operations)
    {
        var context = new PatchOperationContext();
        context.Capture(operations.Select(o => (PatchOperation2Base)new PatchOperation2Combined(o.Name, o.Path)));
        return context;
    }

    [Fact]
    public void IsCaptured_IsFalseUntilCapture()
    {
        var context = new PatchOperationContext();

        Assert.False(context.IsCaptured);
        Assert.False(context.IsPatched("DisplayName"));

        context.Capture([]);

        Assert.True(context.IsCaptured);
    }

    [Theory]
    [InlineData("displayName", "DisplayName")]
    [InlineData("active", "Active")]
    [InlineData("userName", "UserName")]
    [InlineData("emails[type eq \"work\"].value", "ElectronicMailAddresses[0]:Value")]
    [InlineData("emails", "ElectronicMailAddresses[0]:Value")]
    [InlineData("roles[primary eq \"True\"].value", "Roles[0]:Value")]
    [InlineData("name.givenName", "Name:GivenName")]
    [InlineData("addresses[type eq \"work\"].formatted", "Addresses[0]:Formatted")]
    [InlineData("phoneNumbers[type eq \"work\"].value", "PhoneNumbers[0]:Value")]
    [InlineData(EnterpriseSchema + ":employeeNumber", "EnterpriseExtension:EmployeeNumber")]
    [InlineData(SchemaIdentifiers.Core2KIUser + ":extensionAttribute2", "KIExtension:ExtensionAttribute2")]
    public void IsPatched_MatchesScimPathToSourceValue(string patchPath, string sourceValue)
    {
        var context = Capture((OperationName.Replace, patchPath));

        Assert.True(context.IsPatched(sourceValue));
        Assert.True(context.IsSet(sourceValue));
    }

    [Theory]
    [InlineData("displayName", "ElectronicMailAddresses[0]:Value")]
    [InlineData("name.givenName", "Name:FamilyName")]
    [InlineData("addresses[type eq \"work\"].postalCode", "Addresses[0]:Formatted")]
    [InlineData(EnterpriseSchema + ":department", "EnterpriseExtension:EmployeeNumber")]
    [InlineData(SchemaIdentifiers.Core2KIUser + ":extensionAttribute2", "KIExtension:ExtensionAttribute3")]
    [InlineData("employeeNumber", "EnterpriseExtension:EmployeeNumber")] // core attribute of the same name
    public void IsPatched_IsFalseForOtherAttributes(string patchPath, string sourceValue)
    {
        var context = Capture((OperationName.Replace, patchPath));

        Assert.False(context.IsPatched(sourceValue));
    }

    [Fact]
    public void Remove_IsPatchedButNotSet()
    {
        var context = Capture((OperationName.Remove, "emails[type eq \"work\"].value"));

        Assert.True(context.IsPatched("ElectronicMailAddresses[0]:Value"));
        Assert.False(context.IsSet("ElectronicMailAddresses[0]:Value"));
    }

    [Fact]
    public void PathlessOperation_IsIgnored()
    {
        var context = new PatchOperationContext();
        context.Capture([new PatchOperation2Combined { OperationName = "replace" }]);

        Assert.True(context.IsCaptured);
        Assert.False(context.IsPatched("DisplayName"));
    }

    [Theory]
    [InlineData("Role[0]:Value")] // typo of Roles
    [InlineData("NoSuchProperty")]
    [InlineData("")]
    public void UnknownSourceValue_IsNotPatched(string sourceValue)
    {
        var context = Capture((OperationName.Replace, "roles"), (OperationName.Replace, "displayName"));

        Assert.False(context.IsPatched(sourceValue));
    }

    [Fact]
    public void Capture_ReplacesPreviousOperations()
    {
        var context = Capture((OperationName.Replace, "displayName"));

        context.Capture([new PatchOperation2Combined(OperationName.Replace, "active")]);

        Assert.False(context.IsPatched("DisplayName"));
        Assert.True(context.IsPatched("Active"));
    }

    /// <summary>
    /// Operation as deserialized from a SCIM request body (the path is parsed lazily on first access).
    /// </summary>
    private static PatchOperation2Combined FromJson(string path) =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<PatchOperation2Combined>(
            $$"""{"op":"replace","path":"{{path.Replace("\"", "\\\"")}}","value":"x"}""")!;

    [Fact]
    public void Capture_InvalidPath_IsSkippedWithoutThrowing()
    {
        var invalid = FromJson("emails[[x]]");
        Assert.ThrowsAny<ArgumentException>(() => invalid.Path); // what Core2EnterpriseUser.Apply hits as before

        var context = new PatchOperationContext();
        context.Capture([FromJson("emails[[x]]"), FromJson("displayName")]);

        Assert.True(context.IsCaptured);
        Assert.True(context.IsPatched("DisplayName"));
        Assert.False(context.IsPatched("ElectronicMailAddresses[0]:Value"));
    }

    [Fact]
    public void Capture_DeserializedOperations_AreMatched()
    {
        var context = new PatchOperationContext();
        context.Capture([FromJson("emails[type eq \"work\"].value")]);

        Assert.True(context.IsPatched("ElectronicMailAddresses[0]:Value"));
    }

    [Fact]
    public void Reset_ForgetsCapturedOperations()
    {
        var context = Capture((OperationName.Replace, "active"));

        context.Reset();

        Assert.False(context.IsCaptured);
        Assert.False(context.IsPatched("Active"));
        Assert.False(context.IsSet("Active"));
    }
}
