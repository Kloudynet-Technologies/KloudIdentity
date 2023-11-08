//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.Config
{
    /// <summary>
    /// Attribute to mark a field as sensitive.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SensitiveFieldAttribute : Attribute { }
}
