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
        "LIBMSODBCSQL.17.SO"
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

    // DB2 ODBC Drivers
    public static readonly List<string> PostgreSqlDrivers = new List<string>
    {
        "PSQLODBC35W.DLL",
        "PSQLODBCW.DLL",
        "PSQLODBCW.SO",
        "PODBC35W.DLL"
    };

    public const string DB_NAME_SQLSERVER= "SQL Server";
    public const string DB_NAME_MYSQL = "MySQL";
    public const string DB_NAME_DB2 = "IBM DB2";
    public const string DB_NAME_POSTGRESQL = "Postgre";
    
}
