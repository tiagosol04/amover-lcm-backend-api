using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class CreateMotaRequest
    {
        public int IDModelo { get; set; }
        public string Cor { get; set; } = "";
        public double Quilometragem { get; set; }
        public int Estado { get; set; } = 1;
        public int IDOrdemProducao { get; set; }
        public string NumeroIdentificacao { get; set; } = ""; // VIN
    }

    public class AddPecaSnRequest
    {
        public int IDPeca { get; set; }
        public string NumeroSerie { get; set; } = "";
    }

    [ApiController]
    [Route("api/motas")]
    public class MotasController : ControllerBase
    {
        private readonly LcmContext _db;
        public MotasController(LcmContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Set<Mota>()
                .AsNoTracking()
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

            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var m = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(x => x.IDMota == id)
                .Select(x => new
                {
                    x.IDMota,
                    x.IDModelo,
                    x.IDOrdemProducao,
                    x.NumeroIdentificacao,
                    x.Cor,
                    x.Quilometragem,
                    x.Estado,
                    x.DataRegisto
                })
                .FirstOrDefaultAsync();

            return m == null ? NotFound() : Ok(m);
        }

        // NOVO: procurar por VIN / Nº Identificação
        [HttpGet("by-vin/{vin}")]
        public async Task<IActionResult> GetByVin(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin))
                return BadRequest(new { message = "VIN é obrigatório." });

            var v = vin.Trim();

            var m = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(x => x.NumeroIdentificacao == v)
                .Select(x => new
                {
                    x.IDMota,
                    x.IDModelo,
                    x.IDOrdemProducao,
                    x.NumeroIdentificacao,
                    x.Cor,
                    x.Quilometragem,
                    x.Estado,
                    x.DataRegisto
                })
                .FirstOrDefaultAsync();

            return m == null ? NotFound() : Ok(m);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMotaRequest req)
        {
            if (req.IDModelo <= 0 || req.IDOrdemProducao <= 0 ||
                string.IsNullOrWhiteSpace(req.NumeroIdentificacao) || string.IsNullOrWhiteSpace(req.Cor))
                return BadRequest(new { message = "Campos obrigatórios em falta." });

            // 1:1 com Ordem - evita crash por unique index
            var existsForOrder = await _db.Set<Mota>().AnyAsync(m => m.IDOrdemProducao == req.IDOrdemProducao);
            if (existsForOrder)
                return Conflict(new { message = "Já existe uma mota associada a esta Ordem de Produção." });

            var m = new Mota
            {
                IDModelo = req.IDModelo,
                IDOrdemProducao = req.IDOrdemProducao,
                NumeroIdentificacao = req.NumeroIdentificacao.Trim(),
                Cor = req.Cor.Trim(),
                Quilometragem = req.Quilometragem,
                Estado = req.Estado,
                DataRegisto = DateTime.UtcNow
            };

            _db.Set<Mota>().Add(m);
            await _db.SaveChangesAsync();

            return Ok(new { m.IDMota });
        }

        // GET /api/motas/{id}/pecas-sn
        [HttpGet("{id:int}/pecas-sn")]
        public async Task<IActionResult> GetPecasSn(int id)
        {
            var list = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDMota == id)
                .Join(_db.Set<Peca>().AsNoTracking(),
                      x => x.IDPeca,
                      p => p.IDPeca,
                      (x, p) => new
                      {
                          x.IDMotasPecasSN,
                          x.IDMota,
                          x.IDPeca,
                          p.PartNumber,
                          p.Descricao,
                          x.NumeroSerie
                      })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            return Ok(list);
        }

        // POST /api/motas/{id}/pecas-sn (Upsert)
        [HttpPost("{id:int}/pecas-sn")]
        public async Task<IActionResult> AddOrUpdatePecaSn(int id, [FromBody] AddPecaSnRequest req)
        {
            if (req.IDPeca <= 0 || string.IsNullOrWhiteSpace(req.NumeroSerie))
                return BadRequest(new { message = "IDPeca e NumeroSerie são obrigatórios." });

            var motaExists = await _db.Set<Mota>().AnyAsync(m => m.IDMota == id);
            if (!motaExists) return NotFound(new { message = "Mota não encontrada." });

            var row = await _db.Set<MotasPecasSN>()
                .FirstOrDefaultAsync(x => x.IDMota == id && x.IDPeca == req.IDPeca);

            if (row == null)
            {
                row = new MotasPecasSN
                {
                    IDMota = id,
                    IDPeca = req.IDPeca,
                    NumeroSerie = req.NumeroSerie.Trim()
                };

                _db.Set<MotasPecasSN>().Add(row);
                await _db.SaveChangesAsync();
                return Ok(new { row.IDMotasPecasSN, created = true });
            }

            row.NumeroSerie = req.NumeroSerie.Trim();
            await _db.SaveChangesAsync();
            return Ok(new { row.IDMotasPecasSN, created = false });
        }

        // DELETE /api/motas/pecas-sn/{idMotaPecaSn}
        [HttpDelete("pecas-sn/{idMotaPecaSn:int}")]
        public async Task<IActionResult> DeletePecaSn(int idMotaPecaSn)
        {
            var row = await _db.Set<MotasPecasSN>().FirstOrDefaultAsync(x => x.IDMotasPecasSN == idMotaPecaSn);
            if (row == null) return NotFound();

            _db.Set<MotasPecasSN>().Remove(row);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
