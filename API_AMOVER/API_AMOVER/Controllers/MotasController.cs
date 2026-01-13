using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class UpdateMotaRequest
    {
        public string Cor { get; set; } = "N/A";
        public double Quilometragem { get; set; }
        public int Estado { get; set; }
        public string NumeroIdentificacao { get; set; } = "";
    }

    public class AddMotaPecaSnRequest
    {
        public int IDPeca { get; set; }
        public string NumeroSerie { get; set; } = string.Empty;
    }

    public class CriarServicoRequest
    {
        public int Tipo { get; set; }
        public string? Descricao { get; set; }
        public int Estado { get; set; }
        public string? NotasServico { get; set; }
    }

    [ApiController]
    [Route("api/motas")]
    public class MotasController : ControllerBase
    {
        private readonly LcmContext _db;
        public MotasController(LcmContext db) => _db = db;

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

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMotaRequest req)
        {
            var m = await _db.Set<Mota>().FirstOrDefaultAsync(x => x.IDMota == id);
            if (m == null) return NotFound();

            m.Cor = (req.Cor ?? "N/A").Trim();
            m.Quilometragem = req.Quilometragem;
            m.Estado = req.Estado;
            m.NumeroIdentificacao = (req.NumeroIdentificacao ?? "").Trim();

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ----------- PEÇAS SN NA MOTA -----------
        // GET /api/motas/{id}/pecas-sn
        [HttpGet("{id:int}/pecas-sn")]
        public async Task<IActionResult> GetPecasSn(int id)
        {
            var lista = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDMota == id)
                .Join(_db.Set<Peca>().AsNoTracking(),
                      sn => sn.IDPeca,
                      p => p.IDPeca,
                      (sn, p) => new
                      {
                          sn.IDMotasPecasSN,
                          sn.IDMota,
                          sn.IDPeca,
                          p.PartNumber,
                          p.Descricao,
                          sn.NumeroSerie
                      })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/motas/{id}/pecas-sn
        [HttpPost("{id:int}/pecas-sn")]
        public async Task<IActionResult> AddPecaSn(int id, [FromBody] AddMotaPecaSnRequest req)
        {
            if (req.IDPeca <= 0 || string.IsNullOrWhiteSpace(req.NumeroSerie))
                return BadRequest(new { message = "IDPeca e NumeroSerie são obrigatórios." });

            var motaExists = await _db.Set<Mota>().AnyAsync(m => m.IDMota == id);
            if (!motaExists) return NotFound(new { message = "Mota não encontrada." });

            var sn = new MotasPecasSN
            {
                IDMota = id,
                IDPeca = req.IDPeca,
                NumeroSerie = req.NumeroSerie.Trim()
            };

            _db.Set<MotasPecasSN>().Add(sn);
            await _db.SaveChangesAsync();

            return Ok(new { sn.IDMotasPecasSN });
        }

        [HttpDelete("pecas-sn/{idMotasPecasSn:int}")]
        public async Task<IActionResult> DeletePecaSn(int idMotasPecasSn)
        {
            var sn = await _db.Set<MotasPecasSN>().FirstOrDefaultAsync(x => x.IDMotasPecasSN == idMotasPecasSn);
            if (sn == null) return NotFound();

            _db.Set<MotasPecasSN>().Remove(sn);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ----------- SERVIÇOS NA MOTA -----------
        // GET /api/motas/{id}/servicos
        [HttpGet("{id:int}/servicos")]
        public async Task<IActionResult> GetServicos(int id)
        {
            var lista = await _db.Set<Servico>()
                .AsNoTracking()
                .Where(s => s.IDMota == id)
                .OrderByDescending(s => s.DataServico)
                .Select(s => new
                {
                    s.IDServico,
                    s.IDMota,
                    s.Tipo,
                    s.Descricao,
                    s.Estado,
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico
                })
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/motas/{id}/servicos
        [HttpPost("{id:int}/servicos")]
        public async Task<IActionResult> CreateServico(int id, [FromBody] CriarServicoRequest req)
        {
            var motaExists = await _db.Set<Mota>().AnyAsync(m => m.IDMota == id);
            if (!motaExists) return NotFound(new { message = "Mota não encontrada." });

            var s = new Servico
            {
                IDMota = id,
                Tipo = req.Tipo,
                Descricao = req.Descricao,
                Estado = req.Estado,
                NotasServico = req.NotasServico,
                DataServico = DateTime.UtcNow
            };

            _db.Set<Servico>().Add(s);
            await _db.SaveChangesAsync();

            return Ok(new { s.IDServico });
        }
    }
}
