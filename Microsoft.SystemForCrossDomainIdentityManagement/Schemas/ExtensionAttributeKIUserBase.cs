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
    
    [DataMember(Name = AttributeNames.ExtensionAttribute1, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute1
    {
        get;
        set;
    }
    
    [DataMember(Name = AttributeNames.ExtensionAttribute2, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute2
    {
        get;
        set;
    }
    
    [DataMember(Name = AttributeNames.ExtensionAttribute3, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute3
    {
        get;
        set;
    }
    
    [DataMember(Name = AttributeNames.ExtensionAttribute4, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute4
    {
        get;
        set;
    }
    
    [DataMember(Name = AttributeNames.ExtensionAttribute5, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute5
    {
        get;
        set;
    }
    
    [DataMember(Name = AttributeNames.ExtensionAttribute6, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute6
    {
        get;
        set;
    }
    
    [DataMember(Name = AttributeNames.ExtensionAttribute7, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute7
    {
        get;
        set;
    }

    [DataMember(Name = AttributeNames.ExtensionAttribute8, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute8
    {
        get;
        set;
    }

    [DataMember(Name = AttributeNames.ExtensionAttribute9, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute9
    {
        get;
        set;
    }

    [DataMember(Name = AttributeNames.ExtensionAttribute10, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute10
    {
        get;
        set;
    }

    [DataMember(Name = AttributeNames.ExtensionAttribute11, IsRequired = false, EmitDefaultValue = false)]
    public string ExtensionAttribute11
    {
        get;
        set;
    }
}