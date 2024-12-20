//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.SCIM
{
    using System.Runtime.Serialization;

    [DataContract]
    public abstract class Core2EnterpriseUserBase : Core2UserBase
    {
        protected Core2EnterpriseUserBase()
            : base()
        {
            this.AddSchema(SchemaIdentifiers.Core2EnterpriseUser);
            this.EnterpriseExtension = new ExtensionAttributeEnterpriseUser2();
            this.KIExtension = new ExtensionAttributeKIUser();
        }

        [DataMember(Name = AttributeNames.ExtensionEnterpriseUser2)]
        public ExtensionAttributeEnterpriseUser2 EnterpriseExtension
        {
            get;
            set;
        }
        
        [DataMember(Name = AttributeNames.ExtensionKIUser, IsRequired = false, EmitDefaultValue = false)]
        public ExtensionAttributeKIUser KIExtension
        {
            get;
            set;
        }
    }
}