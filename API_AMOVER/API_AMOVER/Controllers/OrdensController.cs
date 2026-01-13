using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class CriarOrdemFromEncomendaRequest
    {
        public string NumeroOrdem { get; set; } = string.Empty;
        public string PaisDestino { get; set; } = string.Empty;
        public int Estado { get; set; } = 0; // ex: 0=aberta, 1=execução...
    }

    public class UpdateEstadoRequest
    {
        public int Estado { get; set; }
    }

    public class CriarMotaRequest
    {
        public int IDModelo { get; set; }
        public string Cor { get; set; } = "N/A";
        public double Quilometragem { get; set; } = 0;
        public int Estado { get; set; } = 0;
        public string NumeroIdentificacao { get; set; } = "";
    }

    [ApiController]
    [Route("api/ordens")]
    public class OrdensController : ControllerBase
    {
        private readonly LcmContext _db;
        public OrdensController(LcmContext db) => _db = db;

        // GET /api/ordens?estado=1
        [HttpGet]
        public async Task<IActionResult> GetOrdens([FromQuery] int? estado = null)
        {
            var query = _db.Set<OrdemProducao>().AsNoTracking();

            if (estado.HasValue)
                query = query.Where(o => o.Estado == estado.Value);

            var ordens = await query
                .OrderByDescending(o => o.DataCriacao)
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.Estado,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    o.IDEncomenda
                })
                .ToListAsync();

            return Ok(ordens);
        }

        // GET /api/ordens/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .Where(o => o.IDOrdemProducao == id)
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.Estado,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    o.IDEncomenda
                })
                .FirstOrDefaultAsync();

            return ordem == null ? NotFound() : Ok(ordem);
        }

        // POST /api/ordens/from-encomenda/{encomendaId}
        [HttpPost("from-encomenda/{encomendaId:int}")]
        public async Task<IActionResult> CriarFromEncomenda(int encomendaId, [FromBody] CriarOrdemFromEncomendaRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NumeroOrdem) || string.IsNullOrWhiteSpace(req.PaisDestino))
                return BadRequest(new { message = "NumeroOrdem e PaisDestino são obrigatórios." });

            var encomenda = await _db.Set<Encomenda>().AsNoTracking().FirstOrDefaultAsync(e => e.IDEncomenda == encomendaId);
            if (encomenda == null) return NotFound(new { message = "Encomenda não encontrada." });

            var ordem = new OrdemProducao
            {
                IDEncomenda = encomendaId,
                NumeroOrdem = req.NumeroOrdem.Trim(),
                PaisDestino = req.PaisDestino.Trim(),
                Estado = req.Estado,
                DataCriacao = DateTime.UtcNow,

                // ajuda no “ambiente real”
                ClienteIDCliente = encomenda.IDCliente,
                ModeloMotaIDModelo = encomenda.IDModelo
            };

            _db.Set<OrdemProducao>().Add(ordem);
            await _db.SaveChangesAsync();

            return Ok(new { ordem.IDOrdemProducao });
        }

        // PUT /api/ordens/{id}/estado
        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> UpdateEstado(int id, [FromBody] UpdateEstadoRequest req)
        {
            var ordem = await _db.Set<OrdemProducao>().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null) return NotFound();

            ordem.Estado = req.Estado;

            // se marcar como concluída, pode fechar data
            if (req.Estado == 2 && ordem.DataConclusao == null) // ex: 2=concluída
                ordem.DataConclusao = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // GET /api/ordens/{id}/resumo
        [HttpGet("{id:int}/resumo")]
        public async Task<IActionResult> Resumo(int id)
        {
            var ordemExists = await _db.Set<OrdemProducao>().AnyAsync(o => o.IDOrdemProducao == id);
            if (!ordemExists) return NotFound();

            var motas = await _db.Set<Mota>().AsNoTracking().CountAsync(m => m.IDOrdemProducao == id);
            var servicos = await _db.Set<Servico>().AsNoTracking()
                .Join(_db.Set<Mota>().AsNoTracking().Where(m => m.IDOrdemProducao == id),
                      s => s.IDMota,
                      m => m.IDMota,
                      (s, m) => s)
                .CountAsync();

            var montagemOk = await _db.Set<ChecklistMontagem>().AsNoTracking()
                .Where(x => x.IDOrdemProducao == id)
                .AllAsync(x => x.Verificado == 1);

            var embalagemOk = await _db.Set<ChecklistEmbalagem>().AsNoTracking()
                .Where(x => x.IDOrdemProducao == id)
                .AllAsync(x => x.Incluido == 1);

            var controloOk = await _db.Set<ChecklistControlo>().AsNoTracking()
                .Where(x => x.IDOrdemProducao == id)
                .AllAsync(x => x.ControloFinal == 1);

            return Ok(new
            {
                ordemId = id,
                motas,
                servicos,
                checklists = new { montagemOk, embalagemOk, controloOk }
            });
        }

        // GET /api/ordens/{id}/motas
        [HttpGet("{id:int}/motas")]
        public async Task<IActionResult> GetMotasDaOrdem(int id)
        {
            var lista = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(m => m.IDOrdemProducao == id)
                .OrderByDescending(m => m.DataRegisto)
                .Select(m => new
                {
                    m.IDMota,
                    m.IDModelo,
                    m.IDOrdemProducao,
                    m.NumeroIdentificacao,
                    m.Cor,
                    m.Quilometragem,
                    m.Estado,
                    m.DataRegisto
                })
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/ordens/{id}/motas
        [HttpPost("{id:int}/motas")]
        public async Task<IActionResult> CriarMotaNaOrdem(int id, [FromBody] CriarMotaRequest req)
        {
            if (req.IDModelo <= 0) return BadRequest(new { message = "IDModelo é obrigatório." });

            var ordemExists = await _db.Set<OrdemProducao>().AnyAsync(o => o.IDOrdemProducao == id);
            if (!ordemExists) return NotFound(new { message = "Ordem não existe." });

            var mota = new Mota
            {
                IDOrdemProducao = id,
                IDModelo = req.IDModelo,
                Cor = (req.Cor ?? "N/A").Trim(),
                Quilometragem = req.Quilometragem,
                Estado = req.Estado,
                NumeroIdentificacao = (req.NumeroIdentificacao ?? "").Trim(),
                DataRegisto = DateTime.UtcNow
            };

            _db.Set<Mota>().Add(mota);
            await _db.SaveChangesAsync();

            return Ok(new { mota.IDMota });
        }
    }
}
