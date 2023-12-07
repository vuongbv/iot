using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace WE.IOT.Service.Controllers
{
    [Route("api/home")]
    [ApiController]
    //[Authorize(Roles = "authenticated")]
    public class HomeController : ControllerBase
    {
        private Common.Logging.ILogger _logger;

        public HomeController(Common.Logging.ILogger logger)
        {
            this._logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            _logger.Info("hello");

            return Ok();
        }
    }
}
