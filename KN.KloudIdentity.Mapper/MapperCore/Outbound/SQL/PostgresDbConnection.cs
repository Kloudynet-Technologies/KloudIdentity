using KN.KloudIdentity.Mapper.Domain.Mapping;
using System.Data;
using System.Data.Odbc;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;

public class PostgresDbConnection : IDbConnection, IDisposable
{
    private readonly OdbcConnection _connection;

    public PostgresDbConnection(OdbcConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public OdbcCommand CreateCommand(string storedProcedureName, List<OdbcParameter> odbcParameters)
    {
        var command = new OdbcCommand(
            $"CALL {storedProcedureName} ({string.Join(",", Enumerable.Repeat("?", odbcParameters.Count))})", _connection)  
        {
            CommandType = CommandType.Text
        };

        command.Parameters.AddRange(odbcParameters.ToArray());

        return command;
    }

    public OdbcCommand CreateCommand(string storedProcedureName, List<OdbcParameter> odbcParameters, HttpRequestTypes? requestType)
    {
        if(!requestType.HasValue || requestType != HttpRequestTypes.GET)
        {
            return CreateCommand(storedProcedureName, odbcParameters);
        }
        else if (requestType == HttpRequestTypes.GET)
        {
            return CreateCommandForGet(storedProcedureName, odbcParameters);
        }

        throw new NotSupportedException($"Request type {requestType} is not supported." );
    }

    private OdbcCommand CreateCommandForGet(string functionName, List<OdbcParameter> odbcParameters)
    {
        // Build the function call using SELECT
        var command = new OdbcCommand(
            $"SELECT * FROM {functionName}({string.Join(",", Enumerable.Repeat("?", odbcParameters.Count))})",_connection)
        {
            CommandType = CommandType.Text
        };

        command.Parameters.AddRange(odbcParameters.ToArray());

        return command;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
