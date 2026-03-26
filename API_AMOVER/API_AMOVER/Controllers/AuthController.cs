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
        private const int ESTADO_UTILIZADOR_ATIVO = 1;
        private const int ESTADO_UTILIZADOR_INATIVO = 0;

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

            // úteis para a app móvel
            public int? IdUtilizador { get; set; }
            public string? NomeUtilizador { get; set; }
            public int? EstadoUtilizador { get; set; }
            public string? EstadoUtilizadorNome { get; set; }
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "UsernameOrEmail e Password são obrigatórios." });

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
                return Unauthorized(new { message = "Credenciais inválidas." });

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash ?? "", request.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Credenciais inválidas." });

            var roles = user.Roles
                .Select(r => r.Name ?? "")
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var utilizador = await ResolveUtilizadorOperacionalAsync(user);

            if (utilizador != null && utilizador.Estado == ESTADO_UTILIZADOR_INATIVO)
            {
                return Unauthorized(new
                {
                    message = "O utilizador operacional encontra-se inativo."
                });
            }

            var expiresRaw = _config["Jwt:ExpiresInMinutes"] ?? _config["Jwt:ExpiresMinutes"];
            var expiresMinutes = int.TryParse(expiresRaw, out var m) && m > 0 ? m : 120;

            var token = GenerateJwt(user, roles, expiresMinutes, utilizador);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Username = user.UserName ?? "",
                Email = user.Email ?? "",
                Roles = roles,
                ExpiresInMinutes = expiresMinutes,
                IdUtilizador = utilizador?.IdUtilizador,
                NomeUtilizador = utilizador?.Nome,
                EstadoUtilizador = utilizador?.Estado,
                EstadoUtilizadorNome = utilizador == null
                    ? null
                    : GetEstadoUtilizadorNome(utilizador.Estado)
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var authUserId =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            var username = User.FindFirstValue(ClaimTypes.Name);
            var email = User.FindFirstValue(ClaimTypes.Email);

            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Utilizadore? utilizador = null;

            if (!string.IsNullOrWhiteSpace(authUserId) || !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(username))
            {
                utilizador = await _db.Set<Utilizadore>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        (!string.IsNullOrWhiteSpace(authUserId) && u.KeycloakId != null && u.KeycloakId == authUserId) ||
                        (!string.IsNullOrWhiteSpace(email) && u.Email != null && u.Email.ToUpper() == email.ToUpper()) ||
                        (!string.IsNullOrWhiteSpace(username) && u.Email != null && u.Email.ToUpper() == username.ToUpper()));
            }

            return Ok(new
            {
                auth = new
                {
                    userId = authUserId,
                    username,
                    email,
                    roles
                },
                utilizador = utilizador == null ? null : new
                {
                    utilizador.IdUtilizador,
                    utilizador.Nome,
                    utilizador.Email,
                    utilizador.Telefone,
                    utilizador.Estado,
                    estadoNome = GetEstadoUtilizadorNome(utilizador.Estado),
                    utilizador.DataCriacao,
                    utilizador.KeycloakId
                }
            });
        }

        private string GenerateJwt(AspNetUser user, List<string> roles, int expiresInMinutes, Utilizadore? utilizador)
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(key))
                throw new Exception("Jwt:Key não está configurada no appsettings.json");

            if (string.IsNullOrWhiteSpace(issuer))
                throw new Exception("Jwt:Issuer não está configurada no appsettings.json");

            if (string.IsNullOrWhiteSpace(audience))
                throw new Exception("Jwt:Audience não está configurada no appsettings.json");

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

            if (utilizador != null)
            {
                claims.Add(new Claim("utilizador_id", utilizador.IdUtilizador.ToString()));
                claims.Add(new Claim("utilizador_estado", utilizador.Estado.ToString()));
            }

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

        private async Task<Utilizadore?> ResolveUtilizadorOperacionalAsync(AspNetUser user)
        {
            var userId = user.Id;
            var email = user.Email;
            var userName = user.UserName;

            return await _db.Set<Utilizadore>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.KeycloakId != null && u.KeycloakId == userId) ||
                    (email != null && u.Email != null && u.Email.ToUpper() == email.ToUpper()) ||
                    (userName != null && u.Email != null && u.Email.ToUpper() == userName.ToUpper()));
        }

        private static string GetEstadoUtilizadorNome(int estado)
        {
            return estado switch
            {
                ESTADO_UTILIZADOR_ATIVO => "Ativo",
                ESTADO_UTILIZADOR_INATIVO => "Inativo",
                _ => "Desconhecido"
            };
        }
    }
}