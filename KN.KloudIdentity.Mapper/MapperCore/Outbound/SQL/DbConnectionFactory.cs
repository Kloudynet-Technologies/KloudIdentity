using System.Data.Odbc;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;

public class DbConnectionFactory
{
    public static IDbConnection Create(OdbcConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        string driverName = connection.Driver.ToUpper();

        // Extend this switch case to support multiple database connections if needed
        //@ToDo
        return driverName switch
        {
            "MSODBCSQL17.DLL" => new GenericDbConnection(connection),
            "SQLSRV32.DLL" => new GenericDbConnection(connection),
            "SQLNCLIRDA11.DLL" => new GenericDbConnection(connection),
            "MYODBC8W.DLL" => new GenericDbConnection(connection),
            _ => throw new NotSupportedException($"ODBC Driver '{driverName}' is not supported.")
        };
    }
}