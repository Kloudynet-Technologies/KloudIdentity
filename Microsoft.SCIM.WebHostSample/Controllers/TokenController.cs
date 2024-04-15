//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.SCIM.WebHostSample.Controllers
{
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using System.Text;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.IdentityModel.Tokens;

    // Controller for generating a bearer token for authorization during testing.
    // This is not meant to replace proper Oauth for authentication purposes.
    [Route("scim/token")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly IConfiguration configuration;
        private const int defaultTokenExpirationTimeInMins = 120;

        public TokenController(IConfiguration Configuration)
        {
            this.configuration = Configuration;
        }

        private string GenerateJSONWebToken()
        {
            var section = this.configuration.GetSection("KI");

            SymmetricSecurityKey securityKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Token:IssuerSigningKey"]));
            SigningCredentials credentials =
                new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            DateTime startTime = DateTime.UtcNow;
            DateTime expiryTime;
            if (double.TryParse(section["Token:TokenLifetimeInMins"], out double tokenExpiration))
                expiryTime = startTime.AddMinutes(tokenExpiration);
            else
                expiryTime = startTime.AddMinutes(defaultTokenExpirationTimeInMins);

            JwtSecurityToken token =
                new JwtSecurityToken(
                    section["Token:TokenIssuer"],
                    section["Token:TokenAudience"],
                    null,
                    notBefore: startTime,
                    expires: expiryTime,
                    signingCredentials: credentials);

            string result = new JwtSecurityTokenHandler().WriteToken(token);
            return result;
        }

        [HttpGet]
        public ActionResult Get()
        {
            string tokenString = this.GenerateJSONWebToken();
            return this.Ok(new { token = tokenString });
        }

    }
}
