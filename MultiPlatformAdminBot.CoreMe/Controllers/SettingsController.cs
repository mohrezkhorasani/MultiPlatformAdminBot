using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using MediatR;
using System.Linq;
using System.Threading.Tasks;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly IMediator _mediator;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public SettingsController(IAppDbContext db, IMediator mediator, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _db = db;
            _mediator = mediator;
            _cache = cache;
        }

        [HttpGet("mode")]
        public IActionResult GetAppMode()
        {
            var setting = _db.SystemSettings.FirstOrDefault(s => s.Key == "AppMode");
            var mode = setting?.Value ?? "2"; // Default to Production
            return Ok(new { success = true, mode = int.Parse(mode) });
        }

        [HttpPost("mode")]
        public async Task<IActionResult> SetAppMode([FromQuery] int mode)
        {
            if (mode != 1 && mode != 2)
                return BadRequest(new { success = false, message = "Invalid mode. Use 1 for Debug, 2 for Production." });

            var setting = _db.SystemSettings.FirstOrDefault(s => s.Key == "AppMode");
            if (setting == null)
            {
                setting = new global::Domain.Entity.SystemSettingTBL { Key = "AppMode", Value = mode.ToString() };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = mode.ToString();
            }

            await _db.SaveChangesAsync();

            await _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.AppModeChangedNotification());

            return Ok(new { success = true, message = $"App mode set to {(mode == 1 ? "Debug" : "Production")}." });
        }

        [HttpPost("clear-cache")]
        public IActionResult ClearCache()
        {
            _cache.Remove("AppMode");
            _cache.Remove("AllPlatformConfigs");
            _cache.Remove("AllAdminChannels");

            // You can also use MemoryCache Compact if it was castable, but removing known keys is safer
            if (_cache is Microsoft.Extensions.Caching.Memory.MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0);
            }

            return Ok(new { success = true, message = "All cache cleared successfully." });
        }
    }
}
