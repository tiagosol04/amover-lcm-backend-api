using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace API_AMOVER.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly LcmContext _db;
        private readonly IConfiguration _config;

        public AuthController(LcmContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // DTOs (podem ficar aqui para simplicidade)
        public class LoginRequest
        {
            public string? UsernameOrEmail { get; set; }
            public string? Password { get; set; }
        }

        public class LoginNfcRequest
        {
            // UID do NFC (ou outro identificador que vocês decidirem)
            public string? NfcUid { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.UsernameOrEmail) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Username/Email e Password são obrigatórios." });

            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expiresMinutesStr = _config["Jwt:ExpiresMinutes"];

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
                return StatusCode(500, new { message = "Configuração JWT em falta (Jwt:Key/Issuer/Audience)." });

            int expiresMinutes = 120;
            if (!string.IsNullOrWhiteSpace(expiresMinutesStr) && int.TryParse(expiresMinutesStr, out var m))
                expiresMinutes = m;

            // 1) procurar utilizador por Email ou UserName
            var usernameOrEmail = req.UsernameOrEmail.Trim();

            var user = await _db.AspNetUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Email == usernameOrEmail ||
                    u.UserName == usernameOrEmail ||
                    (u.NormalizedEmail != null && u.NormalizedEmail == usernameOrEmail.ToUpperInvariant()) ||
                    (u.NormalizedUserName != null && u.NormalizedUserName == usernameOrEmail.ToUpperInvariant())
                );

            if (user == null)
                return Unauthorized(new { message = "Credenciais inválidas." });

            // 2) validar password hashed (Identity)
            var hasher = new PasswordHasher<AspNetUser>();
            var hash = user.PasswordHash ?? "";

            var verify = hasher.VerifyHashedPassword(user, hash, req.Password);
            if (verify == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Credenciais inválidas." });

            // 3) ir buscar roles do utilizador (AspNetUserRoles + AspNetRoles)
            // Como o scaffold normalmente não gera entidade AspNetUserRole,
            // fazemos query com FromSqlRaw (simples e robusto).
            var roles = await GetUserRoles(user.Id);

            // 4) gerar token
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(expiresMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim("uid", user.Id),
            };

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                token = jwt,
                expiresAtUtc = expiresAt,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    email = user.Email,
                    roles
                }
            });
        }

        // NFC login (exemplo “real”)
        // Precisas de uma forma de mapear NFC -> utilizador.
        // O ideal é teres uma tabela tipo Utilizadores (tua) com NfcUid,
        // ou uma tabela AspNetUserClaims com claimType="nfc_uid".
        [HttpPost("login-nfc")]
        public async Task<IActionResult> LoginNfc([FromBody] LoginNfcRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NfcUid))
                return BadRequest(new { message = "NfcUid é obrigatório." });

            // Exemplo: NFC guardado como Claim no Identity (AspNetUserClaims)
            var nfc = req.NfcUid.Trim();

            var userId = await _db.AspNetUserClaims
                .AsNoTracking()
                .Where(c => c.ClaimType == "nfc_uid" && c.ClaimValue == nfc)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "NFC não associado a nenhum utilizador." });

            var user = await _db.AspNetUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Unauthorized(new { message = "Utilizador não encontrado." });

            // gerar token igual ao login normal (sem password)
            var fakeReq = new LoginRequest { UsernameOrEmail = user.Email ?? user.UserName, Password = "N/A" };
            return await IssueTokenForUser(user);
        }

        private async Task<IActionResult> IssueTokenForUser(AspNetUser user)
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expiresMinutesStr = _config["Jwt:ExpiresMinutes"];

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
                return StatusCode(500, new { message = "Configuração JWT em falta (Jwt:Key/Issuer/Audience)." });

            int expiresMinutes = 120;
            if (!string.IsNullOrWhiteSpace(expiresMinutesStr) && int.TryParse(expiresMinutesStr, out var m))
                expiresMinutes = m;

            var roles = await GetUserRoles(user.Id);

            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(expiresMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? user.Email ?? user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim("uid", user.Id),
            };

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                token = jwt,
                expiresAtUtc = expiresAt,
                user = new
                {
                    id = user.Id,
                    username = user.UserName,
                    email = user.Email,
                    roles
                }
            });
        }

        private async Task<List<string>> GetUserRoles(string userId)
        {
            // Query direta ao join AspNetUserRoles -> AspNetRoles
            // (evita ter de criar model AspNetUserRole)
            var roles = await _db.AspNetRoles
                .FromSqlRaw(@"
                    SELECT r.*
                    FROM AspNetRoles r
                    INNER JOIN AspNetUserRoles ur ON ur.RoleId = r.Id
                    WHERE ur.UserId = {0}
                ", userId)
                .AsNoTracking()
                .Select(r => r.Name ?? "")
                .Where(n => n != "")
                .ToListAsync();

            return roles;
        }
    }
}
