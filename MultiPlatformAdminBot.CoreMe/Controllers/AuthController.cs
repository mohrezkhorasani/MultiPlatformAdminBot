using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Domain.Entity;
using Microsoft.IdentityModel.Tokens;
using MultiPlatformAdminBot.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Linq;

namespace MultiPlatformAdminBot.CoreMe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAppDbContext _db;
        private readonly JwtOptions _jwtOptions;

        public AuthController(IAppDbContext db, JwtOptions jwtOptions)
        {
            _db = db;
            _jwtOptions = jwtOptions;
        }

        public class LoginRequest { public long Username { get; set; } public string Password { get; set; } = ""; }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var admin = _db.AdminUserTBLs.FirstOrDefault(u => u.ID == request.Username);
            if (admin == null) return Unauthorized(new { message = "Invalid credentials" });
            if (!admin.HasPanelAccess) return Unauthorized(new { message = "You do not have panel access" });

            bool passwordOk = PasswordHasher.Verify(request.Password, admin.PasswordHash);
            if (!passwordOk)
            {
                // Self-heal: if the stored value is a pre-migration plaintext,
                // verify directly and upgrade the hash in place.
                if (PasswordHasher.IsLegacyPlaintext(admin.PasswordHash) && admin.PasswordHash == request.Password)
                {
                    admin.PasswordHash = PasswordHasher.Hash(request.Password);
                    _db.SaveChanges();
                    passwordOk = true;
                }
            }
            if (!passwordOk) return Unauthorized(new { message = "Invalid credentials" });

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtOptions.SigningKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, admin.ID.ToString()) }),
                Expires = System.DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Ok(new { token = tokenHandler.WriteToken(token) });
        }
    }
}
