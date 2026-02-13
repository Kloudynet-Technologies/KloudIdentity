using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public enum AuthOnFailureAction
{
    None = 0,
    Skip = 1,
    Stop = 2
}