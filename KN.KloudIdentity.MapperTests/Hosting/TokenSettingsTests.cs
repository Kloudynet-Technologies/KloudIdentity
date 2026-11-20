using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SCIM.WebHostSample.Configuration;
using Microsoft.SCIM.WebHostSample.Controllers;

namespace KN.KloudIdentity.MapperTests.Hosting;

/// <summary>
/// #421: with App Configuration refresh working, a sentinel bump reloads IConfiguration, but the JWT validation options are
/// built once. Issuing and validating therefore share one startup snapshot of KI:Token (TokenSettings): a reloaded signing
/// key, issuer or audience can never sign tokens that this instance rejects.
/// </summary>
public sealed class TokenSettingsTests
{
    private const string StartupKey = "startup-signing-key-0123456789abcdef0123456789";

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["KI:Token:IssuerSigningKey"] = StartupKey,
        ["KI:Token:TokenIssuer"] = "issuer",
        ["KI:Token:TokenAudience"] = "audience",
        ["KI:Token:AuthUser"] = "scim-user",
        ["KI:Token:AuthPassword"] = "scim-password",
    }).Build();

    private static string IssueToken(TokenSettings settings)
    {
        var controller = new TokenController(settings)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.Headers["X-Tenant-Id"] = "tenant-a";
        var ok = Assert.IsType<OkObjectResult>(controller.Post(new TokenController.TokenRequest { User = "scim-user", Password = "scim-password" }));
        return Assert.IsType<string>(ok.Value!.GetType().GetProperty("token")!.GetValue(ok.Value));
    }

    /// <summary>KI:Token changes after startup (a sentinel bump); the controller keeps issuing with the startup snapshot —
    /// the key, issuer and audience validation was built with — so its tokens still validate.</summary>
    [Fact]
    public void AReloadAfterStartup_DoesNotChangeWhatTheControllerIssues()
    {
        var configuration = Configuration();
        var settings = TokenSettings.From(configuration);
        configuration["KI:Token:IssuerSigningKey"] = "rotated-signing-key-0123456789abcdef0123456789";
        configuration["KI:Token:TokenIssuer"] = "rotated-issuer";
        configuration["KI:Token:TokenAudience"] = "rotated-audience";
        configuration["KI:Token:AuthPassword"] = "rotated-password";

        var token = IssueToken(settings);

        new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "issuer",
            ValidateAudience = true, ValidAudience = "audience",
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(StartupKey)),
        }, out _);
    }

    [Fact]
    public void TheSnapshot_NeverPrintsTheKeyOrThePassword()
    {
        var text = new TokenSettings("the-key", "issuer", "audience", "60", "user", "the-password").ToString();

        Assert.DoesNotContain("the-key", text);
        Assert.DoesNotContain("the-password", text);
    }

    /// <summary>Startup validates with the snapshot and registers it for the controller (without it, the controller
    /// cannot be resolved); nothing reads KI:Token from IConfiguration per request.</summary>
    [Fact]
    public void StartupAndTheController_UseTheSnapshot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Microsoft.SCIM.WebHostSample", "Startup.cs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var startup = File.ReadAllText(Path.Combine(dir!.FullName, "Microsoft.SCIM.WebHostSample", "Startup.cs"));
        var controller = File.ReadAllText(Path.Combine(dir.FullName, "Microsoft.SCIM.WebHostSample", "Controllers", "TokenController.cs"));

        Assert.Contains("var tokenSettings = TokenSettings.From(this.configuration);", startup);
        Assert.Contains("services.AddSingleton(tokenSettings);", startup);
        var jwtOptions = startup[startup.IndexOf("void ConfigureJwtBearerOptons", StringComparison.Ordinal)..
                                 startup.IndexOf("services.AddOptions<AppSettings>()", StringComparison.Ordinal)];
        Assert.DoesNotContain("configuration", jwtOptions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tokenSettings.IssuerSigningKey", jwtOptions);
        Assert.DoesNotContain("using Microsoft.Extensions.Configuration;", controller);
        Assert.DoesNotContain("GetSection(", controller);
    }
}
