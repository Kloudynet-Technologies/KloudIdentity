using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SCIM.WebHostSample.Controllers
{
    [Route("scim/config")]
    [ApiController]
    [Authorize]
    public class ConfigController : ControllerBase
    {
        private readonly IConfigReader _configReader;

        public ConfigController(IConfigReader configReader)
        {
            _configReader = configReader;
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
    }
}
