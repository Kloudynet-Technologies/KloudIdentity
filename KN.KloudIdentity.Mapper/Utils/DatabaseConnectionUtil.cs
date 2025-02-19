using KN.KloudIdentity.Mapper.Domain.Authentication;
using System;
using System.Collections.Generic;
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
            var driver when driver.Contains("SQL Server", StringComparison.OrdinalIgnoreCase) =>
                GetSqlServerConnectionString(authMethod),

            var driver when driver.Contains("IBM DB2", StringComparison.OrdinalIgnoreCase) =>
                GetDb2ConnectionString(authMethod),

            var driver when driver.Contains("MySQL", StringComparison.OrdinalIgnoreCase) =>
                GetMySqlConnectionString(authMethod),

            _ => throw new NotSupportedException($"Unsupported database driver: {authMethod.Driver}")

        };
    }

    private static string GetSqlServerConnectionString(SQLAuthentication authMethod)
    {
        return $"Driver={authMethod.Driver};Server={authMethod.Server};" +
               $"Database={authMethod.Database};Uid={authMethod.UID};Pwd={authMethod.PWD};";
    }

    private static string GetMySqlConnectionString(SQLAuthentication authMethod)
    {
        return $"Driver={authMethod.Driver};Server={authMethod.Server};Database={authMethod.Database};" +
               $"UId={authMethod.UID};Password={authMethod.PWD};";
    }

    private static string GetDb2ConnectionString(SQLAuthentication authMethod)
    {
        if (authMethod.AdditionalProperties == null)
            throw new ArgumentException("AdditionalProperties cannot be null for DB2 connection.");

        if (!authMethod.AdditionalProperties.TryGetValue("port", out string? port) || string.IsNullOrWhiteSpace(port))
            throw new ArgumentException("Port must be specified in AdditionalProperties.");

        if (!authMethod.AdditionalProperties.TryGetValue("protocol", out string? protocol) || string.IsNullOrWhiteSpace(protocol))
            throw new ArgumentException("Protocol must be specified in AdditionalProperties.");


        return $"Driver={authMethod.Driver};Hostname={authMethod.Server};" +
                   $"Port={port};Protocol={protocol};Database={authMethod.Database};" +
                   $"Uid={authMethod.UID};Pwd={authMethod.PWD};";
    }
}
