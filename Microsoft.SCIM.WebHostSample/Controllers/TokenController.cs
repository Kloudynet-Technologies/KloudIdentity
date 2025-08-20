using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.SCIM.WebHostSample.Controllers
{
    [Route("scim/token")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private const int DefaultTokenExpirationTimeInMins = 120;

        public TokenController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public class TokenRequest
        {
            [Required]
            public string User { get; set; }

            [Required]
            [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
            public string Password { get; set; }
        }

        private string GenerateJsonWebToken()
        {
            var section = configuration.GetSection("KI");

            var signingKey = section["Token:IssuerSigningKey"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException("IssuerSigningKey configuration value is missing or empty.");
            }
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "scim-service"),
                new Claim(ClaimTypes.Role, "TokenGenerator")
            };

            var startTime = DateTime.UtcNow;
            var expiryTime = startTime.AddMinutes(
                double.TryParse(section["Token:TokenLifetimeInMins"], out double tokenExpiration)
                ? tokenExpiration
                : DefaultTokenExpirationTimeInMins);

            var token = new JwtSecurityToken(
                section["Token:TokenIssuer"],
                section["Token:TokenAudience"],
                claims,
                notBefore: startTime,
                expires: expiryTime,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost]
        public ActionResult Post([FromBody] TokenRequest request)
        {
            var section = configuration.GetSection("KI:Token");

            var expectedUser = section["AuthUser"];
            var expectedPassword = section["AuthPassword"];

            if (request.User != expectedUser || !IsPasswordValid(request.Password, expectedPassword))
            {
                return Unauthorized("Invalid credentials");
            }

            var tokenString = this.GenerateJsonWebToken();
            return Ok(new { token = tokenString });
        }
        
        private static bool IsPasswordValid(string providedPassword, string expectedPassword)
        {
            if (providedPassword == null || expectedPassword == null)
                return false;

            var providedBytes = Encoding.UTF8.GetBytes(providedPassword);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedPassword);

            return providedBytes.Length == expectedBytes.Length &&
                   System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}