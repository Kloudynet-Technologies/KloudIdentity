//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SCIM.WebHostSample.Controllers
{
    [Route("scim/config")]
    [ApiController]
    // [Authorize]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigReader _configReader;
        private readonly IGetVerifiedAttributeMapping _getVerifiedAttributeMapping;

        public ConfigController(IConfigReader configReader, IGetVerifiedAttributeMapping getVerifiedAttributeMapping)
        {
            _configReader = configReader;
            _getVerifiedAttributeMapping = getVerifiedAttributeMapping;
        }

        [HttpGet("{appId}")]
        public async Task<ActionResult<MapperConfig>> Get(
            string appId,
            CancellationToken cancellationToken
        )
        {
            var config = await _configReader.GetConfigAsync(appId, cancellationToken);

            return Ok(config);
        }

        [HttpPost]
        public async Task<ActionResult> Post(
            [FromBody] MapperConfig config,
            CancellationToken cancellationToken
        )
        {
            await _configReader.CreateConfigAsync(config, cancellationToken);

            return Ok();
        }

        [HttpPut]
        public async Task<ActionResult> Put(
            [FromBody] MapperConfig config,
            CancellationToken cancellationToken
        )
        {
            await _configReader.UpdateConfigAsync(config, cancellationToken);

            return Ok();
        }

        [HttpGet("verify/{appId}")]
        public async Task<ActionResult<MapperConfig>> GetVarifiedMappingValue(
          string appId,
          ObjectTypes type,
          SCIMDirections direction,
          HttpRequestTypes method

      )
        {
            var json = await _getVerifiedAttributeMapping.GetVerifiedAsync(appId, type, direction, method);

            return Ok(json);
        }
    }
}
