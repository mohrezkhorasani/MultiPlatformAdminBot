using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Domain.Entity;
using MediatR;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    public class CreateChannelDto
    {
        public string? PlatformName { get; set; }
        public long? BotTokenId { get; set; }
        public string? TargetChannelId { get; set; }
        public bool IsActive { get; set; }
        public int Type { get; set; } = 1; // 1 for Debug, 2 for Production
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChannelController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly IMediator _mediator;

        public ChannelController(IAppDbContext db, IMediator mediator)
        {
            _db = db;
            _mediator = mediator;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var channels = _db.PlatformConfigTBLs.Select(c => new {
                c.ID, c.PlatformName, c.BotTokenId, c.TargetChannelId, c.IsActive, c.Type
            }).ToList();
            return Ok(channels);
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateChannelDto dto)
        {
            var config = new PlatformConfigTBL
            {
                PlatformName = dto.PlatformName,
                BotTokenId = dto.BotTokenId,
                TargetChannelId = dto.TargetChannelId,
                IsActive = dto.IsActive,
                DateInsert = DateTime.UtcNow,
                Status = 1,
                Type = dto.Type == 0 ? 1 : dto.Type
            };
            _db.PlatformConfigTBLs.Add(config);
            _db.SaveChanges();
            _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.PlatformConfigChangedNotification()).Wait();
            return Ok(config);
        }

        [HttpPut("{id}")]
        public IActionResult Update(long id, [FromBody] CreateChannelDto dto)
        {
            var existing = _db.PlatformConfigTBLs.FirstOrDefault(c => c.ID == id);
            if (existing == null) return NotFound();

            existing.PlatformName = dto.PlatformName;
            existing.BotTokenId = dto.BotTokenId;
            existing.TargetChannelId = dto.TargetChannelId;
            existing.IsActive = dto.IsActive;
            if (dto.Type != 0) existing.Type = dto.Type;

            _db.SaveChanges();
            _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.PlatformConfigChangedNotification()).Wait();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(long id)
        {
            var existing = _db.PlatformConfigTBLs.FirstOrDefault(c => c.ID == id);
            if (existing == null) return NotFound();

            // First delete mappings
            var mappings = _db.AdminChannelTBLs.Where(ac => ac.ChannelId == id).ToList();
            _db.AdminChannelTBLs.RemoveRange(mappings);

            _db.PlatformConfigTBLs.Remove(existing);
            _db.SaveChanges();
            _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.PlatformConfigChangedNotification()).Wait();
            return Ok();
        }
    }
}
