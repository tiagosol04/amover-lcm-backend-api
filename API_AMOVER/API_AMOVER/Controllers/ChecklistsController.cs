using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    public class ChecklistCreateRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int Tipo { get; set; } // 1=Montagem, 2=Embalagem, 3=Controlo (exemplo)
    }

    public class UpdateFlagRequest
    {
        public int Value { get; set; } // 0/1
    }

    [ApiController]
    [Route("api/checklists")]
    public class ChecklistsController : ControllerBase
    {
        private readonly LcmContext _db;
        public ChecklistsController(LcmContext db) => _db = db;

        // -------- templates --------
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Set<Checklist>()
                .AsNoTracking()
                .OrderBy(c => c.Tipo)
                .ThenBy(c => c.Nome)
                .Select(c => new { c.IDChecklist, c.Nome, c.Descricao, c.Tipo })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChecklistCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "Nome é obrigatório." });

            var c = new Checklist
            {
                Nome = req.Nome.Trim(),
                Descricao = (req.Descricao ?? "").Trim(),
                Tipo = req.Tipo
            };

            _db.Set<Checklist>().Add(c);
            await _db.SaveChangesAsync();

            return Ok(new { c.IDChecklist });
        }

        // -------- execução por ordem --------
        // GET /api/ordens/{ordemId}/checklists
        [HttpGet("/api/ordens/{ordemId:int}/checklists")]
        public async Task<IActionResult> GetByOrdem(int ordemId)
        {
            var montagem = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == ordemId)
                .Join(_db.Set<Checklist>().AsNoTracking(),
                      x => x.IDChecklist,
                      c => c.IDChecklist,
                      (x, c) => new { x.IDChecklist, c.Nome, c.Tipo, Verificado = x.Verificado })
                .ToListAsync();

            var embalagem = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == ordemId)
                .Join(_db.Set<Checklist>().AsNoTracking(),
                      x => x.IDChecklist,
                      c => c.IDChecklist,
                      (x, c) => new { x.IDChecklist, c.Nome, c.Tipo, Incluido = x.Incluido })
                .ToListAsync();

            var controlo = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == ordemId)
                .Join(_db.Set<Checklist>().AsNoTracking(),
                      x => x.IDChecklist,
                      c => c.IDChecklist,
                      (x, c) => new { x.IDChecklist, c.Nome, c.Tipo, ControloFinal = x.ControloFinal })
                .ToListAsync();

            return Ok(new { ordemId, montagem, embalagem, controlo });
        }

        // PUT /api/ordens/{ordemId}/checklists/montagem/{checklistId}
        [HttpPut("/api/ordens/{ordemId:int}/checklists/montagem/{checklistId:int}")]
        public async Task<IActionResult> SetMontagem(int ordemId, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            var row = await _db.Set<ChecklistMontagem>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == ordemId && x.IDChecklist == checklistId);

            if (row == null)
            {
                row = new ChecklistMontagem
                {
                    IDOrdemProducao = ordemId,
                    IDChecklist = checklistId,
                    Verificado = req.Value
                };
                _db.Set<ChecklistMontagem>().Add(row);
            }
            else
            {
                row.Verificado = req.Value;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PUT /api/ordens/{ordemId}/checklists/embalagem/{checklistId}
        [HttpPut("/api/ordens/{ordemId:int}/checklists/embalagem/{checklistId:int}")]
        public async Task<IActionResult> SetEmbalagem(int ordemId, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            var row = await _db.Set<ChecklistEmbalagem>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == ordemId && x.IDChecklist == checklistId);

            if (row == null)
            {
                row = new ChecklistEmbalagem
                {
                    IDOrdemProducao = ordemId,
                    IDChecklist = checklistId,
                    Incluido = req.Value
                };
                _db.Set<ChecklistEmbalagem>().Add(row);
            }
            else
            {
                row.Incluido = req.Value;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // PUT /api/ordens/{ordemId}/checklists/controlo/{checklistId}
        [HttpPut("/api/ordens/{ordemId:int}/checklists/controlo/{checklistId:int}")]
        public async Task<IActionResult> SetControlo(int ordemId, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            var row = await _db.Set<ChecklistControlo>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == ordemId && x.IDChecklist == checklistId);

            if (row == null)
            {
                row = new ChecklistControlo
                {
                    IDOrdemProducao = ordemId,
                    IDChecklist = checklistId,
                    ControloFinal = req.Value
                };
                _db.Set<ChecklistControlo>().Add(row);
            }
            else
            {
                row.ControloFinal = req.Value;
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
