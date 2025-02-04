using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;

public interface IDbConnection
{
    OdbcCommand CreateCommand(string storedProcedureName, List<OdbcParameter> odbcParameters);
}
