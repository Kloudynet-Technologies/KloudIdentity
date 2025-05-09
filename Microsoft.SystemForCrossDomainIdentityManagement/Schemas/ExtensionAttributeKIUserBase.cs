using System.Runtime.Serialization;

namespace Microsoft.SCIM;

[DataContract]
public abstract class ExtensionAttributeKIUserBase
{
    [DataMember(Name = AttributeNames.GroupProfile, IsRequired = false, EmitDefaultValue = false)]
    public string GroupProfile
    {
        get;
        set;
    }

    [DataMember(Name = AttributeNames.SupplementalGroupProfile, IsRequired = false, EmitDefaultValue = false)]
    public string SupplementalGroupProfile
    {
        get;
        set;
    }
}