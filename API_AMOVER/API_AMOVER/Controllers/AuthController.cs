using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API_AMOVER.Data;
using API_AMOVER.Data.Models;
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

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("UsernameOrEmail e Password são obrigatórios.");

            // ✅ Buscar utilizador + Roles SEM usar AspNetUserRoles (não existe no teu DbContext)
            var user = await _db.AspNetUsers
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u =>
                    u.UserName == request.UsernameOrEmail ||
                    u.Email == request.UsernameOrEmail ||
                    u.NormalizedUserName == request.UsernameOrEmail.ToUpper() ||
                    u.NormalizedEmail == request.UsernameOrEmail.ToUpper()
                );

            if (user == null)
                return Unauthorized("Credenciais inválidas.");

            // Verificar password (Identity hash)
            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash ?? "", request.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Credenciais inválidas.");

            var roles = user.Roles.Select(r => r.Name ?? "").Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
            var expiresMinutes = int.TryParse(_config["Jwt:ExpiresInMinutes"], out var m) ? m : 120;

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

            // ✅ Nada de BinaryReader aqui
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
