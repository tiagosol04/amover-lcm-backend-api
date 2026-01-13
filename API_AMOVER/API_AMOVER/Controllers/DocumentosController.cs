using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class DocumentoCreateRequest
    {
        public string Nome { get; set; } = string.Empty;
    }

    public class DocumentoModeloCreateRequest
    {
        public int IDDocumento { get; set; }
        public string Caminho { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/documentos")]
    public class DocumentosController : ControllerBase
    {
        private readonly LcmContext _db;
        public DocumentosController(LcmContext db) => _db = db;

        // ----------- Documento base -----------
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var docs = await _db.Set<Documento>()
                .AsNoTracking()
                .OrderBy(d => d.Nome)
                .Select(d => new { d.IDDocumento, d.Nome })
                .ToListAsync();

            return Ok(docs);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DocumentoCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "Nome é obrigatório." });

            var d = new Documento { Nome = req.Nome.Trim() };
            _db.Set<Documento>().Add(d);
            await _db.SaveChangesAsync();
            return Ok(new { d.IDDocumento });
        }

        // ----------- Documentos por Modelo -----------
        // GET /api/documentos/modelo/{idModelo}
        [HttpGet("modelo/{idModelo:int}")]
        public async Task<IActionResult> GetByModelo(int idModelo)
        {
            var lista = await _db.Set<DocumentosModelo>()
                .AsNoTracking()
                .Where(dm => dm.IDModelo == idModelo)
                .Join(_db.Set<Documento>().AsNoTracking(),
                      dm => dm.IDDocumento,
                      doc => doc.IDDocumento,
                      (dm, doc) => new
                      {
                          dm.IDDocumentosModelo,
                          dm.IDModelo,
                          dm.IDDocumento,
                          doc.Nome,
                          dm.Caminho
                      })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/documentos/modelo/{idModelo}
        [HttpPost("modelo/{idModelo:int}")]
        public async Task<IActionResult> AddToModelo(int idModelo, [FromBody] DocumentoModeloCreateRequest req)
        {
            if (req.IDDocumento <= 0 || string.IsNullOrWhiteSpace(req.Caminho))
                return BadRequest(new { message = "IDDocumento e Caminho são obrigatórios." });

            var exists = await _db.Set<DocumentosModelo>()
                .AnyAsync(x => x.IDModelo == idModelo && x.IDDocumento == req.IDDocumento);

            if (exists) return Conflict(new { message = "Documento já associado a este modelo." });

            var dm = new DocumentosModelo
            {
                IDModelo = idModelo,
                IDDocumento = req.IDDocumento,
                Caminho = req.Caminho.Trim()
            };

            _db.Set<DocumentosModelo>().Add(dm);
            await _db.SaveChangesAsync();

            return Ok(new { dm.IDDocumentosModelo });
        }

        // DELETE /api/documentos/modelo/{idModelo}/{idAssoc}
        [HttpDelete("modelo/{idModelo:int}/{idAssoc:int}")]
        public async Task<IActionResult> RemoveFromModelo(int idModelo, int idAssoc)
        {
            var dm = await _db.Set<DocumentosModelo>()
                .FirstOrDefaultAsync(x => x.IDDocumentosModelo == idAssoc && x.IDModelo == idModelo);

            if (dm == null) return NotFound();

            _db.Set<DocumentosModelo>().Remove(dm);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
