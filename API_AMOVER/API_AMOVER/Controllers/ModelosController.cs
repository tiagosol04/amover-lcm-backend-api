using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class ModeloCreateRequest
    {
        public string CodigoProduto { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public DateTime DataInicioProducao { get; set; } = DateTime.UtcNow;
        public DateTime? DataLancamento { get; set; }
        public DateTime? DataDescontinuacao { get; set; }
        public int Estado { get; set; }
    }

    public class AddPecaModeloRequest
    {
        public int IDPeca { get; set; }
        public string? EspecificacaoPadrao { get; set; }
    }

    public class AddChecklistModeloRequest
    {
        public int IDChecklist { get; set; }
    }

    [ApiController]
    [Route("api/modelos")]
    public class ModelosController : ControllerBase
    {
        private readonly LcmContext _db;
        public ModelosController(LcmContext db) => _db = db;

        // GET /api/modelos?estado=1
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? estado = null)
        {
            var q = _db.Set<ModelosMotum>().AsNoTracking();

            if (estado.HasValue)
                q = q.Where(m => m.Estado == estado.Value);

            var modelos = await q
                .OrderBy(m => m.Nome)
                .Select(m => new
                {
                    m.IDModelo,
                    m.CodigoProduto,
                    m.Nome,
                    m.Estado,
                    m.DataInicioProducao,
                    m.DataLancamento,
                    m.DataDescontinuacao
                })
                .ToListAsync();

            return Ok(modelos);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var m = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .Where(x => x.IDModelo == id)
                .Select(x => new
                {
                    x.IDModelo,
                    x.CodigoProduto,
                    x.Nome,
                    x.Estado,
                    x.DataInicioProducao,
                    x.DataLancamento,
                    x.DataDescontinuacao
                })
                .FirstOrDefaultAsync();

            return m == null ? NotFound() : Ok(m);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ModeloCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CodigoProduto) || string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "CodigoProduto e Nome são obrigatórios." });

            var m = new ModelosMotum
            {
                CodigoProduto = req.CodigoProduto.Trim(),
                Nome = req.Nome.Trim(),
                Estado = req.Estado,
                DataInicioProducao = req.DataInicioProducao,
                DataLancamento = req.DataLancamento,
                DataDescontinuacao = req.DataDescontinuacao
            };

            _db.Set<ModelosMotum>().Add(m);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = m.IDModelo }, new { m.IDModelo });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ModeloCreateRequest req)
        {
            var m = await _db.Set<ModelosMotum>().FirstOrDefaultAsync(x => x.IDModelo == id);
            if (m == null) return NotFound();

            if (string.IsNullOrWhiteSpace(req.CodigoProduto) || string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "CodigoProduto e Nome são obrigatórios." });

            m.CodigoProduto = req.CodigoProduto.Trim();
            m.Nome = req.Nome.Trim();
            m.Estado = req.Estado;
            m.DataInicioProducao = req.DataInicioProducao;
            m.DataLancamento = req.DataLancamento;
            m.DataDescontinuacao = req.DataDescontinuacao;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ------------------- PEÇAS FIXAS DO MODELO -------------------
        // GET /api/modelos/{id}/pecas-fixas
        [HttpGet("{id:int}/pecas-fixas")]
        public async Task<IActionResult> GetPecasFixas(int id)
        {
            var lista = await _db.Set<ModeloPecasFixa>()
                .AsNoTracking()
                .Where(x => x.IDModelo == id)
                .Join(_db.Set<Peca>().AsNoTracking(),
                      mpf => mpf.IDPeca,
                      p => p.IDPeca,
                      (mpf, p) => new
                      {
                          mpf.IDMPF,
                          mpf.IDModelo,
                          mpf.IDPeca,
                          p.PartNumber,
                          p.Descricao,
                          mpf.EspecificacaoPadrao
                      })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/modelos/{id}/pecas-fixas
        [HttpPost("{id:int}/pecas-fixas")]
        public async Task<IActionResult> AddPecaFixa(int id, [FromBody] AddPecaModeloRequest req)
        {
            var exists = await _db.Set<ModeloPecasFixa>()
                .AnyAsync(x => x.IDModelo == id && x.IDPeca == req.IDPeca);

            if (exists) return Conflict(new { message = "Essa peça já está associada como fixa a este modelo." });

            var mpf = new ModeloPecasFixa
            {
                IDModelo = id,
                IDPeca = req.IDPeca,
                EspecificacaoPadrao = req.EspecificacaoPadrao
            };

            _db.Set<ModeloPecasFixa>().Add(mpf);
            await _db.SaveChangesAsync();

            return Ok(new { mpf.IDMPF });
        }

        // DELETE /api/modelos/{id}/pecas-fixas/{idmpf}
        [HttpDelete("{id:int}/pecas-fixas/{idmpf:int}")]
        public async Task<IActionResult> RemovePecaFixa(int id, int idmpf)
        {
            var mpf = await _db.Set<ModeloPecasFixa>()
                .FirstOrDefaultAsync(x => x.IDMPF == idmpf && x.IDModelo == id);

            if (mpf == null) return NotFound();

            _db.Set<ModeloPecasFixa>().Remove(mpf);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ------------------- PEÇAS SERIALIZADAS DO MODELO -------------------
        // GET /api/modelos/{id}/pecas-sn
        [HttpGet("{id:int}/pecas-sn")]
        public async Task<IActionResult> GetPecasSn(int id)
        {
            var lista = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDModelo == id)
                .Join(_db.Set<Peca>().AsNoTracking(),
                      mpsn => mpsn.IDPeca,
                      p => p.IDPeca,
                      (mpsn, p) => new
                      {
                          mpsn.IDModeloPSN,
                          mpsn.IDModelo,
                          mpsn.IDPeca,
                          p.PartNumber,
                          p.Descricao,
                          mpsn.EspecificacaoPadrao
                      })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/modelos/{id}/pecas-sn
        [HttpPost("{id:int}/pecas-sn")]
        public async Task<IActionResult> AddPecaSn(int id, [FromBody] AddPecaModeloRequest req)
        {
            var exists = await _db.Set<ModeloPecasSN>()
                .AnyAsync(x => x.IDModelo == id && x.IDPeca == req.IDPeca);

            if (exists) return Conflict(new { message = "Essa peça já está associada como SN a este modelo." });

            var mpsn = new ModeloPecasSN
            {
                IDModelo = id,
                IDPeca = req.IDPeca,
                EspecificacaoPadrao = req.EspecificacaoPadrao
            };

            _db.Set<ModeloPecasSN>().Add(mpsn);
            await _db.SaveChangesAsync();

            return Ok(new { mpsn.IDModeloPSN });
        }

        // DELETE /api/modelos/{id}/pecas-sn/{idModeloPsn}
        [HttpDelete("{id:int}/pecas-sn/{idModeloPsn:int}")]
        public async Task<IActionResult> RemovePecaSn(int id, int idModeloPsn)
        {
            var mpsn = await _db.Set<ModeloPecasSN>()
                .FirstOrDefaultAsync(x => x.IDModeloPSN == idModeloPsn && x.IDModelo == id);

            if (mpsn == null) return NotFound();

            _db.Set<ModeloPecasSN>().Remove(mpsn);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ------------------- CHECKLISTS DO MODELO (templates) -------------------
        // GET /api/modelos/{id}/checklists
        [HttpGet("{id:int}/checklists")]
        public async Task<IActionResult> GetChecklistsModelo(int id)
        {
            var lista = await _db.Set<ChecklistModelo>()
                .AsNoTracking()
                .Where(x => x.IDModelo == id)
                .Join(_db.Set<Checklist>().AsNoTracking(),
                      cm => cm.IDChecklist,
                      c => c.IDChecklist,
                      (cm, c) => new
                      {
                          cm.ID,
                          cm.IDModelo,
                          cm.IDChecklist,
                          c.Nome,
                          c.Descricao,
                          c.Tipo
                      })
                .OrderBy(x => x.Tipo)
                .ThenBy(x => x.Nome)
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/modelos/{id}/checklists
        [HttpPost("{id:int}/checklists")]
        public async Task<IActionResult> AddChecklistModelo(int id, [FromBody] AddChecklistModeloRequest req)
        {
            var exists = await _db.Set<ChecklistModelo>()
                .AnyAsync(x => x.IDModelo == id && x.IDChecklist == req.IDChecklist);

            if (exists) return Conflict(new { message = "Esse checklist já está associado ao modelo." });

            var cm = new ChecklistModelo
            {
                IDModelo = id,
                IDChecklist = req.IDChecklist
            };

            _db.Set<ChecklistModelo>().Add(cm);
            await _db.SaveChangesAsync();

            return Ok(new { cm.ID });
        }

        // DELETE /api/modelos/{id}/checklists/{idAssoc}
        [HttpDelete("{id:int}/checklists/{idAssoc:int}")]
        public async Task<IActionResult> RemoveChecklistModelo(int id, int idAssoc)
        {
            var cm = await _db.Set<ChecklistModelo>()
                .FirstOrDefaultAsync(x => x.ID == idAssoc && x.IDModelo == id);

            if (cm == null) return NotFound();

            _db.Set<ChecklistModelo>().Remove(cm);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
