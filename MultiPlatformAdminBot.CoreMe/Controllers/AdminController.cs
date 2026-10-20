using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Domain.Entity;
using MediatR;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly IMediator _mediator;

        public AdminController(IAppDbContext db, IMediator mediator)
        {
            _db = db;
            _mediator = mediator;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var admins = _db.AdminUserTBLs.Select(a => new {
                a.ID, a.Username, a.FullName, a.Explain, a.Status, a.FooterText,
                ChannelIds = _db.AdminChannelTBLs.Where(ac => ac.AdminUserId == a.ID).Select(ac => ac.ChannelId).ToList()
            }).ToList();
            return Ok(admins);
        }

        public class CreateAdminDto
        {
            public long ID { get; set; }
            public string Explain { get; set; }
            public string Password { get; set; }
            public string FooterText { get; set; }
            public List<long> ChannelIds { get; set; }
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateAdminDto dto)
        {
            var admin = new AdminUserTBL { 
                ID = dto.ID, 
                Explain = dto.Explain, 
                PasswordHash = dto.Password, 
                FooterText = dto.FooterText,
                DateInsert = DateTime.UtcNow, 
                Status = 100, 
                Type = 1 
            };
            _db.AdminUserTBLs.Add(admin);
            _db.SaveChanges();

            if (dto.ChannelIds != null)
            {
                foreach(var cId in dto.ChannelIds)
                {
                    _db.AdminChannelTBLs.Add(new AdminChannelTBL { AdminUserId = admin.ID, ChannelId = cId, DateInsert = DateTime.UtcNow, Status = 1, Type = 1 });
                }
                _db.SaveChanges();
            }

            _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.AdminChannelChangedNotification(admin.ID)).Wait();

            return Ok();
        }

        [HttpPut("{id}")]
        public IActionResult Update(long id, [FromBody] CreateAdminDto dto)
        {
            var admin = _db.AdminUserTBLs.FirstOrDefault(a => a.ID == id);
            if (admin == null) return NotFound();

            admin.Explain = dto.Explain;
            admin.FooterText = dto.FooterText;
            if (!string.IsNullOrEmpty(dto.Password)) admin.PasswordHash = dto.Password;

            // Update channels
            var existingChannels = _db.AdminChannelTBLs.Where(ac => ac.AdminUserId == id).ToList();
            _db.AdminChannelTBLs.RemoveRange(existingChannels);
            _db.SaveChanges();

            if (dto.ChannelIds != null)
            {
                foreach(var cId in dto.ChannelIds)
                {
                    _db.AdminChannelTBLs.Add(new AdminChannelTBL { AdminUserId = admin.ID, ChannelId = cId, DateInsert = DateTime.UtcNow, Status = 1, Type = 1 });
                }
                _db.SaveChanges();
            }

            _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.AdminChannelChangedNotification(admin.ID)).Wait();

            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(long id)
        {
            var admin = _db.AdminUserTBLs.FirstOrDefault(a => a.ID == id);
            if (admin == null) return NotFound();

            var mappings = _db.AdminChannelTBLs.Where(ac => ac.AdminUserId == id).ToList();
            _db.AdminChannelTBLs.RemoveRange(mappings);

            _db.AdminUserTBLs.Remove(admin);
            _db.SaveChanges();
            
            _mediator.Publish(new MultiPlatformAdminBot.Application.Notifications.AdminChannelChangedNotification(id)).Wait();
            
            return Ok();
        }
    }
}
