using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Domain.Entity;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TokenController : ControllerBase
    {
        private readonly IAppDbContext _db;

        public TokenController(IAppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var tokens = _db.BotTokenTBLs.Select(t => new {
                t.ID, t.PlatformName, t.TokenValue, t.IsActive
            }).ToList();
            return Ok(tokens);
        }

        [HttpPost]
        public IActionResult Create([FromBody] BotTokenTBL token)
        {
            token.DateInsert = DateTime.UtcNow;
            token.Status = 1;
            token.Type = 1;
            _db.BotTokenTBLs.Add(token);
            _db.SaveChanges();
            return Ok(token);
        }

        [HttpPut("{id}")]
        public IActionResult Update(long id, [FromBody] BotTokenTBL token)
        {
            var existing = _db.BotTokenTBLs.FirstOrDefault(t => t.ID == id);
            if (existing == null) return NotFound();

            existing.PlatformName = token.PlatformName;
            existing.TokenValue = token.TokenValue;
            existing.IsActive = token.IsActive;

            _db.SaveChanges();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(long id)
        {
            var existing = _db.BotTokenTBLs.FirstOrDefault(t => t.ID == id);
            if (existing == null) return NotFound();

            var mappedChannels = _db.PlatformConfigTBLs.Where(c => c.BotTokenId == id).ToList();
            if (mappedChannels.Any())
            {
                return BadRequest(new { message = "Cannot delete token because it is assigned to one or more channels." });
            }

            _db.BotTokenTBLs.Remove(existing);
            _db.SaveChanges();
            return Ok();
        }
    }
}
