using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SCIM.WebHostSample.Controllers;

namespace KN.KloudIdentity.MapperTests.Hosting;

/// <summary>
/// #421: the token API over HTTP, with the configuration loaded through the real App Configuration library — the user
/// name and password as Key Vault references, as in the real store — and JWT validation set up as Startup does: options
/// built from the live KI:Token values, rebuilt on every configuration reload (the change-token source). A sentinel bump
/// then rotates the password (in Key Vault) and the signing key: issuing and validation switch together, with no restart.
/// </summary>
public sealed class TokenApiTests : IAsyncLifetime
{
    private const string Label = "KloudIdentity-Test";
    private const string PasswordUri = "https://scim-vault.vault.azure.net/secrets/scim-auth-password";
    private const string UserUri = "https://scim-vault.vault.azure.net/secrets/scim-auth-user";
    private readonly FakeAppConfigurationStore _store = new();
    private readonly ConcurrentDictionary<string, string> _keyVault = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private IConfigurationRefresher _refresher = null!;

    /// <summary>Lets only the token controller in: the other SCIM controllers need the full host.</summary>
    private sealed class TokenControllerOnly : ControllerFeatureProvider
    {
        protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == typeof(TokenController);
    }

    public async Task InitializeAsync()
    {
        _keyVault[UserUri] = "scim-user";
        _keyVault[PasswordUri] = "first-password";
        _store.Set("KI:RefreshOption", Label, "1");
        _store.Set("KI:Token:IssuerSigningKey", Label, "startup-signing-key-0123456789abcdef0123456789");
        _store.Set("KI:Token:TokenIssuer", Label, "scim-issuer");
        _store.Set("KI:Token:TokenAudience", Label, "scim-audience");
        _store.Set("KI:Token:TokenLifetimeInMins", Label, "60");
        _store.SetKeyVaultReference("KI:Token:AuthUser", Label, UserUri);
        _store.SetKeyVaultReference("KI:Token:AuthPassword", Label, PasswordUri);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddAzureAppConfiguration(options =>
        {
            _store.Connect(options)
                .Select("KI:*", Label)
                .ConfigureKeyVault(kv => kv.SetSecretResolver(uri => new ValueTask<string>(_keyVault[uri.ToString()])))
                .ConfigureRefresh(refresh => refresh.Register("KI:RefreshOption", Label, refreshAll: true)
                    .SetCacheExpiration(TimeSpan.FromSeconds(1)));
            _refresher = options.GetRefresher();
        });

        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new TokenControllerOnly()))
            .AddApplicationPart(typeof(TokenController).Assembly);
        // As Startup (ConfigureJwtBearerOptons, minus the Authority metadata): the options read the live KI:Token values,
        // and the change-token source rebuilds them on every configuration reload.
        var configuration = builder.Configuration;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            var section = configuration.GetSection("KI");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = section["Token:TokenIssuer"],
                ValidateAudience = true, ValidAudience = section["Token:TokenAudience"],
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Token:IssuerSigningKey"]!)),
            };
        });
        builder.Services.AddSingleton<IOptionsChangeTokenSource<JwtBearerOptions>>(
            new ConfigurationChangeTokenSource<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, configuration));
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapGet("/protected", () => "ok").RequireAuthorization();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private async Task<HttpResponseMessage> RequestTokenAsync(string user, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/scim/token") { Content = JsonContent.Create(new { User = user, Password = password }) };
        request.Headers.Add("X-Tenant-Id", "tenant-a");
        return await _client.SendAsync(request);
    }

    private async Task<string> TokenAsync(string password)
    {
        var response = await RequestTokenAsync("scim-user", password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body!["token"];
    }

    private async Task<HttpStatusCode> CallProtectedAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await _client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task ATokenIssuedWithTheKeyVaultCredentials_IsAccepted()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RequestTokenAsync("scim-user", "wrong-password")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, await CallProtectedAsync(await TokenAsync("first-password")));
    }

    /// <summary>The operator rotates the password in Key Vault and the signing key, issuer and audience in App
    /// Configuration, then bumps the sentinel. With no restart: the new password works and the old one is refused; a new
    /// token (signed with the new key) is accepted — before the fix, validation kept the old key and rejected it; and a
    /// token issued before the change is rejected, as after a restart.</summary>
    [Fact]
    public async Task AfterABump_IssuingAndValidationSwitchTogether_WithNoRestart()
    {
        var before = await TokenAsync("first-password");
        Assert.Equal(HttpStatusCode.OK, await CallProtectedAsync(before));

        _keyVault[PasswordUri] = "rotated-password";
        _store.Set("KI:Token:IssuerSigningKey", Label, "rotated-signing-key-0123456789abcdef0123456789");
        _store.Set("KI:Token:TokenIssuer", Label, "scim-issuer-rotated");
        _store.Set("KI:Token:TokenAudience", Label, "scim-audience-rotated");
        _store.Set("KI:RefreshOption", Label, "2");   // the bump
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        await _refresher.RefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await RequestTokenAsync("scim-user", "first-password")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await CallProtectedAsync(await TokenAsync("rotated-password")));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallProtectedAsync(before));
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Microsoft.SCIM.WebHostSample", "Startup.cs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine([dir!.FullName, .. parts]);
    }

    /// <summary>Startup wires what the tests above exercise: the JWT options read the live KI:Token values, and a
    /// configuration change-token source rebuilds them on every reload. Without it, validation keeps the startup values
    /// while TokenController (which reads KI:Token per request) signs with the new ones.</summary>
    [Fact]
    public void Startup_RebuildsTheJwtOptionsOnEveryReload()
    {
        var startup = File.ReadAllText(RepoFile("Microsoft.SCIM.WebHostSample", "Startup.cs"));

        Assert.Contains("new ConfigurationChangeTokenSource<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, configuration)", startup);
        Assert.Contains(".AddJwtBearer(ConfigureJwtBearerOptons)", startup);
        Assert.Contains("var section = this.configuration.GetSection(\"KI\");", startup);
    }
}
