using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class UtilizadorCreateRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = "";
        public int Estado { get; set; } = 1;
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

    [ApiController]
    [Route("api/utilizadores")]
    public class UtilizadoresController : ControllerBase
    {
        private readonly LcmContext _db;
        public UtilizadoresController(LcmContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _db.Set<Utilizadore>()
                .AsNoTracking()
                .OrderBy(u => u.Nome)
                .Select(u => new
                {
                    u.IdUtilizador,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    u.Estado,
                    u.DataCriacao,
                    u.KeycloakId
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UtilizadorCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Email))
                return BadRequest(new { message = "Nome e Email são obrigatórios." });

            var u = new Utilizadore
            {
                Nome = req.Nome.Trim(),
                Email = req.Email.Trim(),
                Telefone = (req.Telefone ?? "").Trim(),
                Estado = req.Estado,
                DataCriacao = DateTime.UtcNow,
                KeycloakId = req.KeycloakId
            };

            _db.Set<Utilizadore>().Add(u);
            await _db.SaveChangesAsync();

            return Ok(new { u.IdUtilizador });
        }

        // GET /api/utilizadores/{id}/motas
        [HttpGet("{id:int}/motas")]
        public async Task<IActionResult> GetMotas(int id)
        {
            var lista = await _db.Set<UtilizadorMotum>()
                .AsNoTracking()
                .Where(um => um.IdUtilizador == id && um.Estado == 1)
                .Join(_db.Set<Mota>().AsNoTracking(),
                    um => um.IDMota,
                    m => m.IDMota,
                    (um, m) => new
                    {
                        um.IDUtilizadorMota,
                        UtilizadorId = um.IdUtilizador,
                        MotaId = um.IDMota,
                        um.DataCriacao,

                        EstadoAssociacao = um.Estado, // <- era "Estado"
                        m.NumeroIdentificacao,
                        m.Cor,
                        EstadoMota = m.Estado,        // <- era "Estado"
                        m.IDOrdemProducao
                    })
                .ToListAsync();

            return Ok(lista);
        }


        // POST /api/utilizadores/{id}/motas
        [HttpPost("{id:int}/motas")]
        public async Task<IActionResult> AssignMota(int id, [FromBody] AssignMotaRequest req)
        {
            if (req.IDMota <= 0) return BadRequest(new { message = "IDMota é obrigatório." });

            var exists = await _db.Set<UtilizadorMotum>()
                .AnyAsync(x => x.IdUtilizador == id && x.IDMota == req.IDMota && x.Estado == 1);

            if (exists) return Conflict(new { message = "Esta mota já está atribuída a este utilizador." });

            var um = new UtilizadorMotum
            {
                IdUtilizador = id,
                IDMota = req.IDMota,
                DataCriacao = DateTime.UtcNow,
                Estado = req.Estado
            };

            _db.Set<UtilizadorMotum>().Add(um);
            await _db.SaveChangesAsync();

            return Ok(new { um.IDUtilizadorMota });
        }

        // PUT /api/utilizadores/motas/{idUtilizadorMota}/inativar
        [HttpPut("motas/{idUtilizadorMota:int}/inativar")]
        public async Task<IActionResult> InativarAssoc(int idUtilizadorMota, [FromBody] InativarAssocRequest req)
        {
            var um = await _db.Set<UtilizadorMotum>().FirstOrDefaultAsync(x => x.IDUtilizadorMota == idUtilizadorMota);
            if (um == null) return NotFound();

            um.Estado = 0;
            um.DataInativacao = DateTime.UtcNow;
            um.MotivoInativacao = req.MotivoInativacao;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
