using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.SQL.Constants;

namespace KN.KloudIdentity.Mapper.Utils;

public  class DatabaseConnectionUtil
{
    public static string GetConnectionString(SQLAuthentication authMethod)
    {
        if (string.IsNullOrWhiteSpace(authMethod.Driver))
            throw new ArgumentNullException(nameof(authMethod.Driver), "Driver name cannot be null or empty.");
        if (authMethod == null)
            throw new ArgumentNullException(nameof(authMethod));

        return authMethod.Driver switch
        {
            var driver when driver.Contains(SQLGlobalConstants.DB_NAME_SQLSERVER, StringComparison.OrdinalIgnoreCase) =>
                GetGenericConnectionString(authMethod),

            var driver when driver.Contains(SQLGlobalConstants.DB_NAME_DB2, StringComparison.OrdinalIgnoreCase) =>
                GetDb2ConnectionString(authMethod),

            var driver when driver.Contains(SQLGlobalConstants.DB_NAME_MYSQL, StringComparison.OrdinalIgnoreCase) =>
                GetGenericConnectionString(authMethod),

            var driver when driver.Contains(SQLGlobalConstants.DB_NAME_POSTGRESQL, StringComparison.OrdinalIgnoreCase) =>
                GetGenericConnectionString(authMethod),

            _ => throw new NotSupportedException($"Unsupported database driver: {authMethod.Driver}")

        };
    }

    private static string GetGenericConnectionString(SQLAuthentication authMethod)
    {
        return $"Driver={authMethod.Driver};Server={authMethod.Server};" +
               $"Database={authMethod.Database};Uid={authMethod.UID};Pwd={authMethod.PWD};";
    }

    private static string GetDb2ConnectionString(SQLAuthentication authMethod)
    {
        return $"Driver={authMethod.Driver};Hostname={authMethod.Server};" +
           $"Database={authMethod.Database};" +
           $"Uid={authMethod.UID};Pwd={authMethod.PWD};";
    }   
}
