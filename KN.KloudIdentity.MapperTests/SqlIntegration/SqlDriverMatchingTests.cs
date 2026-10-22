using KN.KloudIdentity.Mapper.Domain.SQL.Constants;

namespace KN.KloudIdentity.MapperTests;

/// <summary>
/// OdbcConnection.Driver returns the driver's file name, upper-cased by the callers. On Linux (the
/// container image) that name is versioned — the values below are what the published image's
/// msodbcsql17/msodbcsql18 actually report.
/// </summary>
public class SqlDriverMatchingTests
{
    [Theory]
    [InlineData("LIBMSODBCSQL-17.11.SO.1.1")] // Linux msodbcsql17 (container image)
    [InlineData("LIBMSODBCSQL-18.7.SO.1.1")]  // Linux msodbcsql18 (container image)
    [InlineData("LIBMSODBCSQL.17.SO")]
    [InlineData("MSODBCSQL17.DLL")]           // Windows
    [InlineData("MSODBCSQL18.DLL")]           // Windows
    [InlineData("LIBMSODBCSQL.18.DYLIB")]     // macOS
    public void IsDriver_SqlServerDriver_ReturnsTrue(string driverName)
    {
        Assert.True(SQLGlobalConstants.IsDriver(
            driverName, SQLGlobalConstants.SqlServerDrivers, SQLGlobalConstants.SqlServerDriverPrefixes));
    }

    [Theory]
    [InlineData("LIBMYODBC8W.SO")]
    [InlineData("LIBMYODBC9W.SO")]
    public void IsDriver_LinuxMySqlDriver_ReturnsTrue(string driverName)
    {
        Assert.True(SQLGlobalConstants.IsDriver(
            driverName, SQLGlobalConstants.MySqlDrivers, SQLGlobalConstants.MySqlDriverPrefixes));
    }

    [Theory]
    [InlineData("PSQLODBCW.SO")]
    [InlineData("PSQLODBCA.SO")]
    public void IsDriver_LinuxPostgreSqlDriver_ReturnsTrue(string driverName)
    {
        Assert.True(SQLGlobalConstants.IsDriver(
            driverName, SQLGlobalConstants.PostgreSqlDrivers, SQLGlobalConstants.PostgreSqlDriverPrefixes));
    }

    [Theory]
    [InlineData("LIBDB2O.SO")]
    [InlineData("LIBDB2CLIO.SO")]
    public void IsDriver_LinuxDb2Driver_ReturnsTrue(string driverName)
    {
        Assert.True(SQLGlobalConstants.IsDriver(
            driverName, SQLGlobalConstants.Db2Drivers, SQLGlobalConstants.Db2DriverPrefixes));
    }

    [Theory]
    [InlineData("LIBSQLITE3ODBC.SO")]
    [InlineData("LIBMYODBC8W.SO")]
    [InlineData("")]
    public void IsDriver_NonSqlServerDriver_ReturnsFalse(string driverName)
    {
        Assert.False(SQLGlobalConstants.IsDriver(
            driverName, SQLGlobalConstants.SqlServerDrivers, SQLGlobalConstants.SqlServerDriverPrefixes));
    }

    [Fact]
    public void IsDriver_PrefixMustMatchStart_NotSubstring()
    {
        Assert.False(SQLGlobalConstants.IsDriver(
            "XLIBMSODBCSQL-17.11.SO.1.1", SQLGlobalConstants.SqlServerDrivers, SQLGlobalConstants.SqlServerDriverPrefixes));
    }
}
