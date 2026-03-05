using Azure.Core;
using Azure.Identity;
using KN.KloudIdentity.Mapper.Domain.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer;

public static class SqlConnectionFactory
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://database.windows.net/.default"]);
    private static ClientSecretCredential? _credential;

    public static async Task<SqlConnection> CreateAsync(IConfiguration configuration)
    {
        var authMode = configuration["Database:AuthMode"];
        var connString = configuration.GetConnectionString("DefaultConnection");
        if(string.IsNullOrWhiteSpace(connString))
        {
            throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' is missing or empty.");
        }
        if (authMode == "Entra")
        {
            _credential ??= AzureCredentialHelper.CreateClientSecretCredential(configuration);
            var token = await _credential.GetTokenAsync(TokenContext);

            var conn = new SqlConnection(connString);
            conn.AccessToken = token.Token;
            return conn;
        }

        return new SqlConnection(connString);
    }
}