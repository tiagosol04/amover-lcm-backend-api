using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class UpdateServicoEstadoRequest
    {
        public int Estado { get; set; }
        public DateTime? DataConclusao { get; set; }
    }

    public class AddPecaAlteradaRequest
    {
        public int IDMotasPecasSN { get; set; }
        public string? Observacoes { get; set; }
    }

    [ApiController]
    [Route("api/servicos")]
    public class ServicosController : ControllerBase
    {
        private readonly LcmContext _db;
        public ServicosController(LcmContext db) => _db = db;

        // PUT /api/servicos/{id}/estado
        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> UpdateEstado(int id, [FromBody] UpdateServicoEstadoRequest req)
        {
            var s = await _db.Set<Servico>().FirstOrDefaultAsync(x => x.IDServico == id);
            if (s == null) return NotFound();

            s.Estado = req.Estado;

            if (req.DataConclusao.HasValue)
                s.DataConclusao = req.DataConclusao.Value;
            else if (req.Estado == 2 && s.DataConclusao == null) // ex: 2=concluído
                s.DataConclusao = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // GET /api/servicos/{id}/pecas-alteradas
        [HttpGet("{id:int}/pecas-alteradas")]
        public async Task<IActionResult> GetPecasAlteradas(int id)
        {
            var lista = await _db.Set<ServicosPecasAlterada>()
                .AsNoTracking()
                .Where(x => x.IDServico == id)
                .Select(x => new
                {
                    x.ID,
                    x.IDServico,
                    x.IDMotasPecasSN,
                    x.Observacoes
                })
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/servicos/{id}/pecas-alteradas
        [HttpPost("{id:int}/pecas-alteradas")]
        public async Task<IActionResult> AddPecaAlterada(int id, [FromBody] AddPecaAlteradaRequest req)
        {
            if (req.IDMotasPecasSN <= 0)
                return BadRequest(new { message = "IDMotasPecasSN é obrigatório." });

            var servicoExists = await _db.Set<Servico>().AnyAsync(s => s.IDServico == id);
            if (!servicoExists) return NotFound(new { message = "Serviço não encontrado." });

            var assoc = new ServicosPecasAlterada
            {
                IDServico = id,
                IDMotasPecasSN = req.IDMotasPecasSN,
                Observacoes = req.Observacoes
            };

            _db.Set<ServicosPecasAlterada>().Add(assoc);
            await _db.SaveChangesAsync();

            return Ok(new { assoc.ID });
        }

        [HttpDelete("pecas-alteradas/{idAssoc:int}")]
        public async Task<IActionResult> DeleteAssoc(int idAssoc)
        {
            var assoc = await _db.Set<ServicosPecasAlterada>().FirstOrDefaultAsync(x => x.ID == idAssoc);
            if (assoc == null) return NotFound();

            _db.Set<ServicosPecasAlterada>().Remove(assoc);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
