using Microsoft.AspNetCore.Mvc;

namespace WE.IOT.Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthCheckController : Controller
    {
        [HttpGet]
        public async Task<IActionResult> HealthCheck()
        {
            return Ok();
        }

    }
}
