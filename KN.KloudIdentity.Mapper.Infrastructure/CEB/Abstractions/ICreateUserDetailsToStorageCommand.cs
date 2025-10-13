using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Infrastructure.CEB.Abstractions;

public interface ICreateUserDetailsToStorageCommand
{
    Task<bool> CreateUserKeyDataAsync(string userKey, string username);
}
