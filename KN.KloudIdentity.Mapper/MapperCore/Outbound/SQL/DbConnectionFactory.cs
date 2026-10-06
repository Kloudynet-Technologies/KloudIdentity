using KN.KloudIdentity.Mapper.Domain.SQL.Constants;
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
            var driver when SQLGlobalConstants.IsDriver(driver, SQLGlobalConstants.SqlServerDrivers, SQLGlobalConstants.SqlServerDriverPrefixes) => new GenericDbConnection(connection),

            // MySQL ODBC Drivers
            var driver when SQLGlobalConstants.IsDriver(driver, SQLGlobalConstants.MySqlDrivers, SQLGlobalConstants.MySqlDriverPrefixes) => new GenericDbConnection(connection),

            // DB2 ODBC Drivers
            var driver when SQLGlobalConstants.IsDriver(driver, SQLGlobalConstants.Db2Drivers, SQLGlobalConstants.Db2DriverPrefixes) => new GenericDbConnection(connection),

            // Postgresql ODBC Drivers
            var driver when SQLGlobalConstants.IsDriver(driver, SQLGlobalConstants.PostgreSqlDrivers, SQLGlobalConstants.PostgreSqlDriverPrefixes) => new PostgresDbConnection(connection),

            _ => throw new NotSupportedException($"ODBC Driver '{driverName}' is not supported.")
        };
    }
}