using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
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

        public PecasController(LcmContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var pecas = await _db.Set<Peca>()
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .Select(p => new
                {
                    p.IDPeca,
                    p.PartNumber,
                    p.Descricao
                })
                .ToListAsync();

            return Ok(pecas);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var peca = await _db.Set<Peca>()
                .AsNoTracking()
                .Where(x => x.IDPeca == id)
                .Select(x => new
                {
                    x.IDPeca,
                    x.PartNumber,
                    x.Descricao
                })
                .FirstOrDefaultAsync();

            if (peca == null)
                return NotFound(new { message = "Peça não encontrada." });

            return Ok(peca);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PecaCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.PartNumber))
                return BadRequest(new { message = "PartNumber é obrigatório." });

            var partNumber = req.PartNumber.Trim();

            var duplicado = await _db.Set<Peca>()
                .AsNoTracking()
                .AnyAsync(p => p.PartNumber != null && p.PartNumber.ToUpper() == partNumber.ToUpper());

            if (duplicado)
                return Conflict(new { message = "Já existe uma peça com esse PartNumber." });

            var peca = new Peca
            {
                PartNumber = partNumber,
                Descricao = (req.Descricao ?? "").Trim()
            };

            _db.Set<Peca>().Add(peca);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = peca.IDPeca }, new { peca.IDPeca });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PecaCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            var peca = await _db.Set<Peca>().FirstOrDefaultAsync(x => x.IDPeca == id);
            if (peca == null)
                return NotFound(new { message = "Peça não encontrada." });

            if (string.IsNullOrWhiteSpace(req.PartNumber))
                return BadRequest(new { message = "PartNumber é obrigatório." });

            var partNumber = req.PartNumber.Trim();

            var duplicado = await _db.Set<Peca>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IDPeca != id &&
                    x.PartNumber != null &&
                    x.PartNumber.ToUpper() == partNumber.ToUpper());

            if (duplicado)
                return Conflict(new { message = "Já existe outra peça com esse PartNumber." });

            peca.PartNumber = partNumber;
            peca.Descricao = (req.Descricao ?? "").Trim();

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var peca = await _db.Set<Peca>().FirstOrDefaultAsync(x => x.IDPeca == id);
            if (peca == null)
                return NotFound(new { message = "Peça não encontrada." });

            var usadaEmModeloFixo = await _db.Set<ModeloPecasFixa>()
                .AsNoTracking()
                .AnyAsync(x => x.IDPeca == id);

            if (usadaEmModeloFixo)
            {
                return Conflict(new
                {
                    message = "Não é possível apagar esta peça porque está associada como peça fixa a um ou mais modelos."
                });
            }

            var usadaEmModeloSn = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .AnyAsync(x => x.IDPeca == id);

            if (usadaEmModeloSn)
            {
                return Conflict(new
                {
                    message = "Não é possível apagar esta peça porque está associada como peça serializada a um ou mais modelos."
                });
            }

            var usadaEmMotaSn = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .AnyAsync(x => x.IDPeca == id);

            if (usadaEmMotaSn)
            {
                return Conflict(new
                {
                    message = "Não é possível apagar esta peça porque já está associada a motas."
                });
            }

            _db.Set<Peca>().Remove(peca);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}