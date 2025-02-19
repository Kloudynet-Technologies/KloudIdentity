using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;

public class GenericDbConnection : IDbConnection, IDisposable
{
    private readonly OdbcConnection _connection;

    public GenericDbConnection(OdbcConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public OdbcCommand CreateCommand(string storedProcedureName, List<OdbcParameter> odbcParameters)
    {
        var command = new OdbcCommand($"{{CALL {storedProcedureName} ({string.Join(",", Enumerable.Repeat("?", odbcParameters.Count))})}}", _connection) 
                      { 
                        CommandType = CommandType.StoredProcedure 
                      };

        command.Parameters.AddRange(odbcParameters.ToArray());
        
        return command;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}