using Application.Interfaces;
using Application.Interfaces.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeliveryController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly IBroadcastMessageUseCase _useCase;

        public DeliveryController(IAppDbContext db, IBroadcastMessageUseCase useCase)
        {
            _db = db;
            _useCase = useCase;
        }

        /// <summary>Recent failed / pending-retry deliveries (not successfully sent).</summary>
        [HttpGet]
        public async Task<IActionResult> GetFailed()
        {
            var items = await _db.DeliveryLogTBLs
                .Where(d => d.Status != 2)
                .OrderByDescending(d => d.DateInsert)
                .Take(200)
                .Select(d => new
                {
                    d.ID,
                    d.PlatformName,
                    d.TargetChannelId,
                    d.ContentType,
                    d.AttemptCount,
                    d.Status,
                    d.LastError,
                    d.DateInsert
                })
                .ToListAsync();
            return Ok(items);
        }

        /// <summary>Manually retry a failed delivery.</summary>
        [HttpPost("{id}/retry")]
        public async Task<IActionResult> Retry(long id)
        {
            bool ok = await _useCase.RetryDeliveryAsync(id);
            return Ok(new { ok });
        }

        /// <summary>Delivery counters over the last 24h (for dashboards / alerting).</summary>
        [HttpGet("metrics")]
        public async Task<IActionResult> Metrics()
        {
            var since = DateTime.UtcNow.AddDays(-1);
            var rows = await _db.DeliveryLogTBLs
                .Where(d => d.DateInsert >= since)
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var byStatus = rows.ToDictionary(r => r.Status, r => r.Count);
            return Ok(new
            {
                since,
                last24h = new
                {
                    awaitingRetry = byStatus.GetValueOrDefault(1),
                    sent = byStatus.GetValueOrDefault(2),
                    permanentFailure = byStatus.GetValueOrDefault(3)
                }
            });
        }
    }
}
