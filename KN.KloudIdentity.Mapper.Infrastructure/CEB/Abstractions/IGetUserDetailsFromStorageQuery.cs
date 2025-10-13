using KN.KloudIdentity.Mapper.Domain.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Infrastructure.CEB.Abstractions;

public interface IGetUserDetailsFromStorageQuery
{
    Task<UserKeyMappingData?> GetUserKeyDataAsync(string username);
}
