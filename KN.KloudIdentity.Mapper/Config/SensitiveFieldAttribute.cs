using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Config
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SensitiveFieldAttribute : Attribute { }
}
