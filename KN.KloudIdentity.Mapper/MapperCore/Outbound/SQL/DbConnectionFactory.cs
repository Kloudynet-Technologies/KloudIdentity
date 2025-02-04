using System.Data.Odbc;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.SQL;

public class DbConnectionFactory
{
    public static IDbConnection Create(OdbcConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));

        string driverName = connection.Driver.ToUpper();

        // Extend this switch case to support multiple database connections if needed
        return driverName switch
        {
            "MSODBCSQL17.DLL" or "SQLSRV32.DLL" or "SQLNCLIRDA11.DLL" or "MYODBC8W.DLL" => new GenericDbConnection(connection),
            _ => throw new NotSupportedException($"ODBC Driver '{driverName}' is not supported.")
        };
    }
}