using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace API_AMOVER.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly LcmContext _db;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<AspNetUser> _hasher = new();

        public AuthController(LcmContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public class LoginRequest
        {
            public string UsernameOrEmail { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class LoginResponse
        {
            public string Token { get; set; } = "";
            public string UserId { get; set; } = "";
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public List<string> Roles { get; set; } = new();
            public int ExpiresInMinutes { get; set; }
        }

        // ✅ IMPORTANTE: como tens FallbackPolicy no Program.cs, este endpoint tem de ser público
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("UsernameOrEmail e Password são obrigatórios.");

            var input = request.UsernameOrEmail.Trim();
            var inputUpper = input.ToUpperInvariant();

            var user = await _db.AspNetUsers
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u =>
                    u.UserName == input ||
                    u.Email == input ||
                    u.NormalizedUserName == inputUpper ||
                    u.NormalizedEmail == inputUpper
                );

            if (user == null)
                return Unauthorized("Credenciais inválidas.");

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash ?? "", request.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Credenciais inválidas.");

            var roles = user.Roles
                .Select(r => r.Name ?? "")
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .ToList();

            // ✅ Aceita Jwt:ExpiresInMinutes (novo) e Jwt:ExpiresMinutes (antigo)
            var expiresRaw = _config["Jwt:ExpiresInMinutes"] ?? _config["Jwt:ExpiresMinutes"];
            var expiresMinutes = int.TryParse(expiresRaw, out var m) ? m : 120;

            var token = GenerateJwt(user, roles, expiresMinutes);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName ?? "",
                Email = user.Email ?? "",
                Roles = roles,
                ExpiresInMinutes = expiresMinutes
            });
        }

        private string GenerateJwt(AspNetUser user, List<string> roles, int expiresInMinutes)
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(key))
                throw new Exception("Jwt:Key não está configurada no appsettings.json");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
