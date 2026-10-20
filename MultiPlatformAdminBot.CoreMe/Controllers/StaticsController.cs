using Microsoft.AspNetCore.Mvc;
using Application.Interfaces.IInfrastructure;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    public class StaticsController : ControllerBase
    {
        private readonly IEnumerable<IBotPlatformClient> _platformClients;

        public StaticsController(IEnumerable<IBotPlatformClient> platformClients)
        {
            _platformClients = platformClients;
        }

        [Route("/statics.log")]
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> LogStatics()
        {
            var telegramClient = _platformClients.FirstOrDefault(c => c.PlatformName == "Telegram");
            if (telegramClient != null)
            {
                await telegramClient.SendTextMessageAsync("8658914813", "🔔 فراخوانی از `statics.log` دریافت شد!");
            }
            return Ok(new { success = true, message = "Logged successfully." });
        }
    }
}
