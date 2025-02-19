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
            // SQL Server ODBC Drivers
            "MSODBCSQL17.DLL" or
            "SQLSRV32.DLL" or
            "SQLNCLIRDA11.DLL" or
            "LIBMSODBCSQL.17.DYLIB" or
            "LIBMSODBCSQL.17.SO" => new GenericDbConnection(connection),

            // MySQL ODBC Drivers
            "MYODBC8W.DLL" or
            "MYODBC9W.DLL" or
            "LIBMYODBC8W.DYLIB" or
            "LIBMYODBC8W.SO" or
            "LIBMYODBC8A.SO" => new GenericDbConnection(connection),

            // DB2 ODBC Drivers
            "DB2CLI.DLL" or
            "DB2ODBC.DLL" or
            "LIBDB2O.SO" or
            "LIBDB2CLIO.SO" or
            "LIBDB2O.DYLIB" => new GenericDbConnection(connection),

            _ => throw new NotSupportedException($"ODBC Driver '{driverName}' is not supported.")
        };
    }
}