using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class PecaCreateRequest
    {
        public string PartNumber { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/pecas")]
    public class PecasController : ControllerBase
    {
        private readonly LcmContext _db;
        public PecasController(LcmContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var pecas = await _db.Set<Peca>()
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .Select(p => new { p.IDPeca, p.PartNumber, p.Descricao })
                .ToListAsync();

            return Ok(pecas);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var p = await _db.Set<Peca>()
                .AsNoTracking()
                .Where(x => x.IDPeca == id)
                .Select(x => new { x.IDPeca, x.PartNumber, x.Descricao })
                .FirstOrDefaultAsync();

            return p == null ? NotFound() : Ok(p);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PecaCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PartNumber))
                return BadRequest(new { message = "PartNumber é obrigatório." });

            var p = new Peca
            {
                PartNumber = req.PartNumber.Trim(),
                Descricao = (req.Descricao ?? "").Trim()
            };

            _db.Set<Peca>().Add(p);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = p.IDPeca }, new { p.IDPeca });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PecaCreateRequest req)
        {
            var p = await _db.Set<Peca>().FirstOrDefaultAsync(x => x.IDPeca == id);
            if (p == null) return NotFound();

            if (string.IsNullOrWhiteSpace(req.PartNumber))
                return BadRequest(new { message = "PartNumber é obrigatório." });

            p.PartNumber = req.PartNumber.Trim();
            p.Descricao = (req.Descricao ?? "").Trim();

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.Set<Peca>().FirstOrDefaultAsync(x => x.IDPeca == id);
            if (p == null) return NotFound();

            _db.Set<Peca>().Remove(p);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
