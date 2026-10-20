using Application.Interfaces;
using Domain.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IAppDbContext _db;

        public ReportsController(IAppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var reports = await _db.PostReportTBLs
                .Include(r => r.MetaDatas)
                .Include(r => r.AdminChannel)
                    .ThenInclude(ac => ac.AdminUser)
                .Include(r => r.AdminChannel)
                    .ThenInclude(ac => ac.Channel)
                .OrderByDescending(r => r.DateInsert)
                .Select(r => new
                {
                    r.ID,
                    r.IsSuccessful,
                    r.ErrorLog,
                    r.SourceMessageId,
                    r.InOutType,
                    r.DateInsert,
                    AdminUser = r.AdminChannel != null && r.AdminChannel.AdminUser != null ? r.AdminChannel.AdminUser.Username : "نامشخص",
                    ChannelName = r.AdminChannel != null && r.AdminChannel.Channel != null ? r.AdminChannel.Channel.PlatformName : "نامشخص",
                    MetaDatas = r.MetaDatas.Select(m => new
                    {
                        m.MetaKey,
                        m.MetaValue
                    })
                })
                .ToListAsync();

            return Ok(reports);
        }
    }
}
