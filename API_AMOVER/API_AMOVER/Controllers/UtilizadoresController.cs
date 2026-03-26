using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    // -------------------- DTOs --------------------

    public class UtilizadorCreateRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = "";
        public int Estado { get; set; } = 1;

        // Guarda aqui o AspNetUser.Id (string) OU email
        public string? KeycloakId { get; set; }
    }

    public class AssignMotaRequest
    {
        public int IDMota { get; set; }
        public int Estado { get; set; } = 1;
    }

    public class InativarAssocRequest
    {
        public string? MotivoInativacao { get; set; }
    }

    public class UpdateUserStatusRequest
    {
        public bool Ativo { get; set; }
    }

    public class UpdateAuthMappingRequest
    {
        public string AuthUserId { get; set; } = string.Empty;
    }

    // -------------------- Controller --------------------

    [ApiController]
    [Route("api/utilizadores")]
    public class UtilizadoresController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_INATIVO = 0;
        private const int ESTADO_ATIVO = 1;

        public UtilizadoresController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/utilizadores?estado=1&q=joao
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? estado = null, [FromQuery] string? q = null)
        {
            if (estado.HasValue && !EstadoValido(estado.Value))
                return BadRequest(new { message = "Estado inválido. Use 0=Inativo ou 1=Ativo." });

            var query = _db.Set<Utilizadore>().AsNoTracking();

            if (estado.HasValue)
                query = query.Where(u => u.Estado == estado.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var termo = $"%{q.Trim()}%";
                query = query.Where(u =>
                    EF.Functions.Like(u.Nome ?? "", termo) ||
                    EF.Functions.Like(u.Email ?? "", termo) ||
                    EF.Functions.Like(u.Telefone ?? "", termo) ||
                    EF.Functions.Like(u.KeycloakId ?? "", termo));
            }

            var users = await query
                .OrderBy(u => u.Nome)
                .Select(u => new
                {
                    u.IdUtilizador,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    u.Estado,
                    estadoNome = GetEstadoNome(u.Estado),
                    u.DataCriacao,
                    u.KeycloakId
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET /api/utilizadores/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .Where(u => u.IdUtilizador == id)
                .Select(u => new
                {
                    u.IdUtilizador,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    u.Estado,
                    estadoNome = GetEstadoNome(u.Estado),
                    u.DataCriacao,
                    u.KeycloakId
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "Utilizador não encontrado." });

            var totalAssociacoesAtivas = await _db.Set<UtilizadorMotum>()
                .AsNoTracking()
                .CountAsync(x => x.IdUtilizador == id && x.Estado == ESTADO_ATIVO);

            return Ok(new
            {
                utilizador = user,
                totalAssociacoesAtivas
            });
        }

        // POST /api/utilizadores
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UtilizadorCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { message = "Nome e Email são obrigatórios." });

            if (!EstadoValido(req.Estado))
                return BadRequest(new { message = "Estado inválido. Use 0=Inativo ou 1=Ativo." });

            var nome = req.Nome.Trim();
            var email = NormalizeEmail(req.Email);
            var telefone = NormalizeOptional(req.Telefone) ?? "";
            var keycloakId = NormalizeOptional(req.KeycloakId);

            var emailExists = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .AnyAsync(u => u.Email != null && u.Email.ToUpper() == email.ToUpper());

            if (emailExists)
                return Conflict(new { message = "Já existe um utilizador com esse email." });

            if (!string.IsNullOrWhiteSpace(keycloakId))
            {
                var keyExists = await _db.Set<Utilizadore>()
                    .AsNoTracking()
                    .AnyAsync(u => u.KeycloakId == keycloakId);

                if (keyExists)
                    return Conflict(new { message = "Já existe um utilizador com esse mapping auth." });
            }

            var utilizador = new Utilizadore
            {
                Nome = nome,
                Email = email,
                Telefone = telefone,
                Estado = req.Estado,
                DataCriacao = DateTime.UtcNow,
                KeycloakId = keycloakId
            };

            _db.Set<Utilizadore>().Add(utilizador);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                utilizador.IdUtilizador,
                utilizador.Nome,
                utilizador.Email,
                utilizador.Estado,
                estadoNome = GetEstadoNome(utilizador.Estado),
                utilizador.KeycloakId
            });
        }

        // PUT /api/utilizadores/{id}/status
        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateUserStatusRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            var user = await _db.Set<Utilizadore>()
                .FirstOrDefaultAsync(u => u.IdUtilizador == id);

            if (user == null)
                return NotFound(new { message = "Utilizador não encontrado." });

            user.Estado = req.Ativo ? ESTADO_ATIVO : ESTADO_INATIVO;

            if (!req.Ativo)
            {
                var associacoesAtivas = await _db.Set<UtilizadorMotum>()
                    .Where(x => x.IdUtilizador == id && x.Estado == ESTADO_ATIVO)
                    .ToListAsync();

                foreach (var assoc in associacoesAtivas)
                {
                    assoc.Estado = ESTADO_INATIVO;
                    assoc.DataInativacao = DateTime.UtcNow;
                    assoc.MotivoInativacao = "Utilizador inativado.";
                }
            }

            await _db.SaveChangesAsync();

            var totalAssociacoesAtivas = await _db.Set<UtilizadorMotum>()
                .AsNoTracking()
                .CountAsync(x => x.IdUtilizador == id && x.Estado == ESTADO_ATIVO);

            return Ok(new
            {
                message = "Estado atualizado com sucesso.",
                id = user.IdUtilizador,
                estado = user.Estado,
                estadoNome = GetEstadoNome(user.Estado),
                associacoesAtivas = totalAssociacoesAtivas
            });
        }

        // PUT /api/utilizadores/{id}/mapping-auth
        [HttpPut("{id:int}/mapping-auth")]
        public async Task<IActionResult> UpdateAuthMapping(int id, [FromBody] UpdateAuthMappingRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.AuthUserId))
                return BadRequest(new { message = "AuthUserId é obrigatório." });

            var user = await _db.Set<Utilizadore>()
                .FirstOrDefaultAsync(u => u.IdUtilizador == id);

            if (user == null)
                return NotFound(new { message = "Utilizador não encontrado." });

            var authUserId = req.AuthUserId.Trim();

            var keyEmUso = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .AnyAsync(u => u.IdUtilizador != id && u.KeycloakId == authUserId);

            if (keyEmUso)
                return Conflict(new { message = "Esse mapping auth já está associado a outro utilizador." });

            user.KeycloakId = authUserId;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Mapping auth atualizado com sucesso.",
                user.IdUtilizador,
                user.KeycloakId
            });
        }

        // GET /api/utilizadores/by-auth/{authUserId}
        [HttpGet("by-auth/{authUserId}")]
        public async Task<IActionResult> GetByAuth(string authUserId)
        {
            if (string.IsNullOrWhiteSpace(authUserId))
                return BadRequest(new { message = "authUserId é obrigatório." });

            var utilizador = await ResolveUtilizadorByAuthAsync(authUserId.Trim());

            if (utilizador == null)
                return NotFound(new { message = "Utilizador não encontrado (mapping auth/email não resolvido)." });

            return Ok(new
            {
                utilizador.IdUtilizador,
                utilizador.Nome,
                utilizador.Email,
                utilizador.Telefone,
                utilizador.Estado,
                estadoNome = GetEstadoNome(utilizador.Estado),
                utilizador.DataCriacao,
                utilizador.KeycloakId
            });
        }

        // GET /api/utilizadores/by-auth/{authUserId}/motas
        [HttpGet("by-auth/{authUserId}/motas")]
        public async Task<IActionResult> GetMotasByAuth(string authUserId, [FromQuery] bool ativasOnly = true)
        {
            if (string.IsNullOrWhiteSpace(authUserId))
                return BadRequest(new { message = "authUserId é obrigatório." });

            var utilizador = await ResolveUtilizadorByAuthAsync(authUserId.Trim());

            if (utilizador == null)
                return NotFound(new { message = "Utilizador não encontrado (mapping auth/email não resolvido)." });

            return await BuildMotasResponse(utilizador.IdUtilizador, ativasOnly);
        }

        // GET /api/utilizadores/{id}/motas?ativasOnly=true
        [HttpGet("{id:int}/motas")]
        public async Task<IActionResult> GetMotas(int id, [FromQuery] bool ativasOnly = true)
        {
            var userExists = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .AnyAsync(u => u.IdUtilizador == id);

            if (!userExists)
                return NotFound(new { message = "Utilizador não encontrado." });

            return await BuildMotasResponse(id, ativasOnly);
        }

        // GET /api/utilizadores/{id}/motas/historico
        [HttpGet("{id:int}/motas/historico")]
        public async Task<IActionResult> GetMotasHistorico(int id)
        {
            var userExists = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .AnyAsync(u => u.IdUtilizador == id);

            if (!userExists)
                return NotFound(new { message = "Utilizador não encontrado." });

            return await BuildMotasResponse(id, false);
        }

        // POST /api/utilizadores/{id}/motas
        [HttpPost("{id:int}/motas")]
        public async Task<IActionResult> AssignMota(int id, [FromBody] AssignMotaRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.IDMota <= 0)
                return BadRequest(new { message = "IDMota é obrigatório." });

            if (!EstadoValido(req.Estado))
                return BadRequest(new { message = "Estado inválido. Use 0=Inativo ou 1=Ativo." });

            if (req.Estado != ESTADO_ATIVO)
                return BadRequest(new { message = "Ao atribuir uma mota, o estado da associação tem de ser Ativo." });

            var user = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdUtilizador == id);

            if (user == null)
                return NotFound(new { message = "Utilizador não encontrado." });

            if (user.Estado != ESTADO_ATIVO)
                return BadRequest(new { message = "Não é possível atribuir motas a um utilizador inativo." });

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDMota == req.IDMota);

            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var associacaoAtivaMesmoUtilizador = await _db.Set<UtilizadorMotum>()
                .FirstOrDefaultAsync(x => x.IdUtilizador == id && x.IDMota == req.IDMota && x.Estado == ESTADO_ATIVO);

            if (associacaoAtivaMesmoUtilizador != null)
                return Conflict(new { message = "Esta mota já está atribuída a este utilizador." });

            var associacaoAtivaOutroUtilizador = await (
                from assoc in _db.Set<UtilizadorMotum>().AsNoTracking()
                join u in _db.Set<Utilizadore>().AsNoTracking() on assoc.IdUtilizador equals u.IdUtilizador
                where assoc.IDMota == req.IDMota && assoc.Estado == ESTADO_ATIVO && assoc.IdUtilizador != id
                select new
                {
                    assoc.IDUtilizadorMota,
                    assoc.IdUtilizador,
                    u.Nome
                }
            ).FirstOrDefaultAsync();

            if (associacaoAtivaOutroUtilizador != null)
            {
                return Conflict(new
                {
                    message = "Esta mota já está atribuída a outro utilizador ativo.",
                    associacao = associacaoAtivaOutroUtilizador
                });
            }

            var associacaoInativaExistente = await _db.Set<UtilizadorMotum>()
                .FirstOrDefaultAsync(x => x.IdUtilizador == id && x.IDMota == req.IDMota && x.Estado == ESTADO_INATIVO);

            if (associacaoInativaExistente != null)
            {
                associacaoInativaExistente.Estado = ESTADO_ATIVO;
                associacaoInativaExistente.DataInativacao = null;
                associacaoInativaExistente.MotivoInativacao = null;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Associação reativada com sucesso.",
                    associacaoInativaExistente.IDUtilizadorMota,
                    reativada = true
                });
            }

            var novaAssociacao = new UtilizadorMotum
            {
                IdUtilizador = id,
                IDMota = req.IDMota,
                DataCriacao = DateTime.UtcNow,
                Estado = ESTADO_ATIVO
            };

            _db.Set<UtilizadorMotum>().Add(novaAssociacao);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Mota atribuída com sucesso.",
                novaAssociacao.IDUtilizadorMota,
                reativada = false
            });
        }

        // PUT /api/utilizadores/motas/{idUtilizadorMota}/inativar
        [HttpPut("motas/{idUtilizadorMota:int}/inativar")]
        public async Task<IActionResult> InativarAssoc(int idUtilizadorMota, [FromBody] InativarAssocRequest? req)
        {
            var assoc = await _db.Set<UtilizadorMotum>()
                .FirstOrDefaultAsync(x => x.IDUtilizadorMota == idUtilizadorMota);

            if (assoc == null)
                return NotFound(new { message = "Associação Utilizador↔Mota não encontrada." });

            if (assoc.Estado == ESTADO_INATIVO)
            {
                return Ok(new
                {
                    message = "Associação já estava inativa.",
                    id = assoc.IDUtilizadorMota
                });
            }

            assoc.Estado = ESTADO_INATIVO;
            assoc.DataInativacao = DateTime.UtcNow;
            assoc.MotivoInativacao = string.IsNullOrWhiteSpace(req?.MotivoInativacao)
                ? null
                : req!.MotivoInativacao!.Trim();

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Associação inativada com sucesso.",
                id = assoc.IDUtilizadorMota,
                assoc.Estado,
                estadoNome = GetEstadoNome(assoc.Estado),
                assoc.DataInativacao,
                assoc.MotivoInativacao
            });
        }

        private async Task<IActionResult> BuildMotasResponse(int utilizadorId, bool ativasOnly)
        {
            var query =
                from assoc in _db.Set<UtilizadorMotum>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on assoc.IDMota equals m.IDMota
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where assoc.IdUtilizador == utilizadorId
                select new
                {
                    assoc.IDUtilizadorMota,
                    UtilizadorId = assoc.IdUtilizador,
                    MotaId = assoc.IDMota,
                    assoc.DataCriacao,
                    EstadoAssociacao = assoc.Estado,
                    estadoAssociacaoNome = GetEstadoNome(assoc.Estado),
                    assoc.DataInativacao,
                    assoc.MotivoInativacao,
                    m.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(m.NumeroIdentificacao),
                    m.Cor,
                    EstadoMota = m.Estado,
                    m.IDOrdemProducao,
                    m.IDModelo,
                    m.Quilometragem,
                    m.DataRegisto,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                };

            if (ativasOnly)
                query = query.Where(x => x.EstadoAssociacao == ESTADO_ATIVO);

            var lista = await query
                .OrderByDescending(x => x.DataCriacao)
                .ToListAsync();

            return Ok(new
            {
                utilizadorId,
                ativasOnly,
                total = lista.Count,
                motas = lista
            });
        }

        private async Task<Utilizadore?> ResolveUtilizadorByAuthAsync(string authUserId)
        {
            var key = authUserId.Trim();

            var utilizador = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.KeycloakId != null && u.KeycloakId == key) ||
                    (u.Email != null && u.Email.ToUpper() == key.ToUpper()));

            if (utilizador != null)
                return utilizador;

            var aspUser = await _db.Set<AspNetUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == key ||
                    (a.Email != null && a.Email.ToUpper() == key.ToUpper()) ||
                    (a.UserName != null && a.UserName.ToUpper() == key.ToUpper()));

            if (aspUser == null)
                return null;

            var aspId = aspUser.Id;
            var aspEmail = NormalizeOptional(aspUser.Email);
            var aspUserName = NormalizeOptional(aspUser.UserName);

            return await _db.Set<Utilizadore>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.KeycloakId != null && u.KeycloakId == aspId) ||
                    (aspEmail != null && u.Email != null && u.Email.ToUpper() == aspEmail.ToUpper()) ||
                    (aspUserName != null && u.Email != null && u.Email.ToUpper() == aspUserName.ToUpper()));
        }

        private static bool EstadoValido(int estado)
        {
            return estado == ESTADO_INATIVO || estado == ESTADO_ATIVO;
        }

        private static string GetEstadoNome(int estado)
        {
            return estado switch
            {
                ESTADO_ATIVO => "Ativo",
                ESTADO_INATIVO => "Inativo",
                _ => "Desconhecido"
            };
        }

        private static string NormalizeEmail(string value)
        {
            return value.Trim();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}