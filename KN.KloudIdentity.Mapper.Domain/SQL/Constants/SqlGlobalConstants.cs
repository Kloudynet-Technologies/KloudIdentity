using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Domain.SQL.Constants;

public static class SQLGlobalConstants
{
    // SQL Server ODBC Drivers
    public static readonly List<string> SqlServerDrivers = new List<string>
    {
        "MSODBCSQL17.DLL",
        "SQLSRV32.DLL",
        "SQLNCLIRDA11.DLL",
        "LIBMSODBCSQL.17.DYLIB",
        "LIBMSODBCSQL.17.SO",
        "MSODBCSQL18.DLL",
        "LIBMSODBCSQL.18.DYLIB"
    };

    // MySQL ODBC Drivers
    public static readonly List<string> MySqlDrivers = new List<string>
    {
        "MYODBC8W.DLL",
        "MYODBC9W.DLL",
        "LIBMYODBC8W.DYLIB",
        "LIBMYODBC8W.SO",
        "LIBMYODBC8A.SO"
    };

    // DB2 ODBC Drivers
    public static readonly List<string> Db2Drivers = new List<string>
    {
        "DB2CLI.DLL",
        "DB2ODBC.DLL",
        "LIBDB2O.SO",
        "LIBDB2CLIO.SO",
        "LIBDB2O.DYLIB"
    };

    // PostgreSQL ODBC Drivers
    public static readonly List<string> PostgreSqlDrivers = new List<string>
    {
        "PSQLODBC35W.DLL",
        "PSQLODBCW.DLL",
        "PSQLODBCW.SO",
        "PODBC35W.DLL"
    };

    // Linux ODBC driver file-name prefixes. On Linux, OdbcConnection.Driver returns the versioned
    // shared-object file name (e.g. "libmsodbcsql-17.11.so.1.1", "libmsodbcsql-18.7.so.1.1"), which
    // changes with every driver release — so it is matched by prefix, not by the exact names above.
    public static readonly List<string> SqlServerDriverPrefixes = new List<string> { "LIBMSODBCSQL" };
    public static readonly List<string> MySqlDriverPrefixes = new List<string> { "LIBMYODBC" };
    public static readonly List<string> Db2DriverPrefixes = new List<string> { "LIBDB2" };
    public static readonly List<string> PostgreSqlDriverPrefixes = new List<string> { "PSQLODBC" };

    /// <summary>
    /// True if the upper-cased <c>OdbcConnection.Driver</c> value is one of <paramref name="exactNames"/>
    /// (Windows/macOS driver files) or starts with one of <paramref name="linuxPrefixes"/> (versioned
    /// Linux .so files). Shared by DbConnectionFactory and SQLIntegration so both agree.
    /// </summary>
    public static bool IsDriver(string driverName, List<string> exactNames, List<string> linuxPrefixes) =>
        exactNames.Contains(driverName)
        || linuxPrefixes.Any(prefix => driverName.StartsWith(prefix, StringComparison.Ordinal));

    public const string DB_NAME_SQLSERVER= "SQL Server";
    public const string DB_NAME_MYSQL = "MySQL";
    public const string DB_NAME_DB2 = "IBM DB2";
    public const string DB_NAME_POSTGRESQL = "Postgre";
    
}
