using Azure.Identity;
using Microsoft.Graph;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class GraphClientUtil : IGraphClientUtil
{
    public GraphServiceClient GetClient(string tenantId, string clientId, string clientSecret)
    {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(
            tenantId, clientId, clientSecret, options);

        var graphClient = new GraphServiceClient(clientSecretCredential, scopes);

        return graphClient;
    }
}
