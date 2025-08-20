//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
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
            if (!IsValidRequest(request, out var validationResult))
                return validationResult;

            if (!Authenticate(request.User, request.Password))
                return Unauthorized("Invalid credentials");

            var tokenString = GenerateJsonWebToken();
            return Ok(new { token = tokenString });
        }

        // Validation helper
        private bool IsValidRequest(TokenRequest request, out ActionResult result)
        {
            if (request == null)
            {
                result = BadRequest("Request body is missing.");
                return false;
            }

            if (!ModelState.IsValid)
            {
                result = BadRequest(ModelState);
                return false;
            }

            result = null;
            return true;
        }

        // Authentication helper
        private bool Authenticate(string user, string password)
        {
            var section = configuration.GetSection("KI:Token");
            var expectedUser = section["AuthUser"];
            var expectedPassword = section["AuthPassword"];

            if (string.IsNullOrWhiteSpace(expectedUser) || string.IsNullOrWhiteSpace(expectedPassword))
                throw new InvalidOperationException("AuthUser or AuthPassword configuration value is missing or empty.");

            return IsConstantTimeEqual(user, expectedUser) &&
                   IsConstantTimeEqual(password, expectedPassword);
        }

        private static bool IsConstantTimeEqual(string a, string b)
        {
            if (a == null || b == null)
                return false;

            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);

            return aBytes.Length == bBytes.Length &&
                   System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

    }
}