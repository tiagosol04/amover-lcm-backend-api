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
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly LcmContext _context;
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<AspNetUser> _passwordHasher;

        // IMPORTANT: tem de bater certo com o Program.cs
        private const string JwtKeyId = "amover-signing-key-1";

        public AuthController(LcmContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<AspNetUser>();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("usernameOrEmail e password são obrigatórios.");

            var user = await _context.AspNetUsers
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u =>
                    u.UserName == request.UsernameOrEmail ||
                    u.Email == request.UsernameOrEmail);

            if (user == null)
                return Unauthorized("Credenciais inválidas.");

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? "", request.Password);
            if (verify == PasswordVerificationResult.Failed)
                return Unauthorized("Credenciais inválidas.");

            var response = IssueTokenForUser(user);
            return Ok(response);
        }

        // Exemplo (se quiseres mesmo suportar NFC):
        // neste momento está como placeholder (troca a lógica para procurar pelo identificador NFC real)
        [HttpPost("login-nfc")]
        public async Task<IActionResult> LoginNfc([FromBody] NfcLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NfcIdentifier))
                return BadRequest("nfcIdentifier é obrigatório.");

            // TODO: substituir por lookup real (ex.: tabela UtilizadorNfc / campo NfcId)
            var user = await _context.AspNetUsers
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.UserName == request.NfcIdentifier || u.Email == request.NfcIdentifier);

            if (user == null)
                return Unauthorized("Utilizador NFC não encontrado.");

            var response = IssueTokenForUser(user);
            return Ok(response);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest("username, email, password e role são obrigatórios.");
            }

            var usernameExists = await _context.AspNetUsers.AnyAsync(u => u.UserName == request.Username);
            if (usernameExists) return Conflict("Username já existe.");

            var emailExists = await _context.AspNetUsers.AnyAsync(u => u.Email == request.Email);
            if (emailExists) return Conflict("Email já existe.");

            var roleEntity = await _context.AspNetRoles.FirstOrDefaultAsync(r => r.Name == request.Role);
            if (roleEntity == null) return BadRequest("Role inválida (não existe na BD).");

            var user = new AspNetUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = request.Username,
                NormalizedUserName = request.Username.ToUpperInvariant(),
                Email = request.Email,
                NormalizedEmail = request.Email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = false,
                AccessFailedCount = 0
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            user.Roles.Add(roleEntity);

            _context.AspNetUsers.Add(user);
            await _context.SaveChangesAsync();

            var response = IssueTokenForUser(user);
            return Created("api/auth/register", response);
        }

        private AuthResponse IssueTokenForUser(AspNetUser user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];
            var expiresInMinutes = int.TryParse(_configuration["Jwt:ExpiresInMinutes"], out var m) ? m : 120;

            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new InvalidOperationException("Jwt:Key não está configurado.");

            var roles = user.Roles
                .Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? ""),
                new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role!));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            {
                KeyId = JwtKeyId
            };

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return new AuthResponse
            {
                Token = tokenString,
                UserId = user.Id,
                Username = user.UserName ?? "",
                Email = user.Email ?? "",
                Roles = roles!,
                ExpiresInMinutes = expiresInMinutes
            };
        }

        public class LoginRequest
        {
            public string UsernameOrEmail { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class NfcLoginRequest
        {
            public string NfcIdentifier { get; set; } = "";
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string Role { get; set; } = "";
        }

        public class AuthResponse
        {
            public string Token { get; set; } = "";
            public string UserId { get; set; } = "";
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public List<string> Roles { get; set; } = new();
            public int ExpiresInMinutes { get; set; }
        }
    }
}
