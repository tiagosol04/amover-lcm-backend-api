using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class ChecklistCreateRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int Tipo { get; set; } // 1=Montagem, 2=Embalagem, 3=Controlo
    }

    public class UpdateFlagRequest
    {
        public int Value { get; set; } // 0/1
    }

    public class ChecklistItemDto
    {
        public int IDChecklist { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public int Tipo { get; set; }
        public int Value { get; set; }
    }

    public class ChecklistSecaoResumoDto
    {
        public int Total { get; set; }
        public int Feitos { get; set; }
        public bool Inicializado { get; set; }
        public bool Ok { get; set; }
    }

    public class ChecklistExecucaoResponseDto
    {
        public int OrdemId { get; set; }
        public ChecklistSecaoResumoDto ResumoMontagem { get; set; } = new();
        public ChecklistSecaoResumoDto ResumoEmbalagem { get; set; } = new();
        public ChecklistSecaoResumoDto ResumoControlo { get; set; } = new();
        public bool Inicializado { get; set; }
        public bool TudoOk { get; set; }
        public List<ChecklistItemDto> Montagem { get; set; } = new();
        public List<ChecklistItemDto> Embalagem { get; set; } = new();
        public List<ChecklistItemDto> Controlo { get; set; } = new();
    }

    [ApiController]
    [Route("api/checklists")]
    public class ChecklistsController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int TIPO_MONTAGEM = 1;
        private const int TIPO_EMBALAGEM = 2;
        private const int TIPO_CONTROLO = 3;

        public ChecklistsController(LcmContext db)
        {
            _db = db;
        }

        // -------- templates --------

        // GET /api/checklists?tipo=1
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? tipo = null)
        {
            var query = _db.Set<Checklist>().AsNoTracking();

            if (tipo.HasValue)
            {
                if (!TipoValido(tipo.Value))
                    return BadRequest(new { message = "Tipo inválido. Use 1=Montagem, 2=Embalagem, 3=Controlo." });

                query = query.Where(c => c.Tipo == tipo.Value);
            }

            var list = await query
                .OrderBy(c => c.Tipo)
                .ThenBy(c => c.Nome)
                .Select(c => new
                {
                    c.IDChecklist,
                    c.Nome,
                    c.Descricao,
                    c.Tipo
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST /api/checklists
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChecklistCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "Nome é obrigatório." });

            if (!TipoValido(req.Tipo))
                return BadRequest(new { message = "Tipo inválido. Use 1=Montagem, 2=Embalagem, 3=Controlo." });

            var nome = req.Nome.Trim();
            var descricao = (req.Descricao ?? string.Empty).Trim();

            var existe = await _db.Set<Checklist>()
                .AsNoTracking()
                .AnyAsync(c => c.Nome != null && c.Nome.ToUpper() == nome.ToUpper() && c.Tipo == req.Tipo);

            if (existe)
                return Conflict(new { message = "Já existe um checklist com esse nome para esse tipo." });

            var checklist = new Checklist
            {
                Nome = nome,
                Descricao = descricao,
                Tipo = req.Tipo
            };

            _db.Set<Checklist>().Add(checklist);
            await _db.SaveChangesAsync();

            return Ok(new { checklist.IDChecklist });
        }

        // -------- execução por ordem --------

        // GET /api/ordens/{ordemId}/checklists
        [HttpGet("/api/ordens/{ordemId:int}/checklists")]
        public async Task<IActionResult> GetByOrdem(int ordemId)
        {
            var ordemExiste = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.IDOrdemProducao == ordemId);

            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var response = await GetChecklistExecutionData(ordemId);
            return Ok(response);
        }

        // PUT /api/ordens/{ordemId}/checklists/montagem/{checklistId}
        [HttpPut("/api/ordens/{ordemId:int}/checklists/montagem/{checklistId:int}")]
        public async Task<IActionResult> SetMontagem(int ordemId, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (!FlagValida(req.Value))
                return BadRequest(new { message = "O valor tem de ser 0 ou 1." });

            var validacao = await ValidarAtualizacaoChecklist(ordemId, checklistId, TIPO_MONTAGEM);
            if (validacao != null)
                return validacao;

            var row = await _db.Set<ChecklistMontagem>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == ordemId && x.IDChecklist == checklistId);

            if (row == null)
            {
                return BadRequest(new
                {
                    message = "Checklist de montagem não inicializado para esta ordem. Inicia a ordem primeiro."
                });
            }

            row.Verificado = req.Value;
            await _db.SaveChangesAsync();

            return Ok(await BuildChecklistUpdateResponse(ordemId, "montagem", checklistId, req.Value));
        }

        // PUT /api/ordens/{ordemId}/checklists/embalagem/{checklistId}
        [HttpPut("/api/ordens/{ordemId:int}/checklists/embalagem/{checklistId:int}")]
        public async Task<IActionResult> SetEmbalagem(int ordemId, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (!FlagValida(req.Value))
                return BadRequest(new { message = "O valor tem de ser 0 ou 1." });

            var validacao = await ValidarAtualizacaoChecklist(ordemId, checklistId, TIPO_EMBALAGEM);
            if (validacao != null)
                return validacao;

            var row = await _db.Set<ChecklistEmbalagem>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == ordemId && x.IDChecklist == checklistId);

            if (row == null)
            {
                return BadRequest(new
                {
                    message = "Checklist de embalagem não inicializado para esta ordem. Inicia a ordem primeiro."
                });
            }

            row.Incluido = req.Value;
            await _db.SaveChangesAsync();

            return Ok(await BuildChecklistUpdateResponse(ordemId, "embalagem", checklistId, req.Value));
        }

        // PUT /api/ordens/{ordemId}/checklists/controlo/{checklistId}
        [HttpPut("/api/ordens/{ordemId:int}/checklists/controlo/{checklistId:int}")]
        public async Task<IActionResult> SetControlo(int ordemId, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (!FlagValida(req.Value))
                return BadRequest(new { message = "O valor tem de ser 0 ou 1." });

            var validacao = await ValidarAtualizacaoChecklist(ordemId, checklistId, TIPO_CONTROLO);
            if (validacao != null)
                return validacao;

            var row = await _db.Set<ChecklistControlo>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == ordemId && x.IDChecklist == checklistId);

            if (row == null)
            {
                return BadRequest(new
                {
                    message = "Checklist de controlo não inicializado para esta ordem. Inicia a ordem primeiro."
                });
            }

            row.ControloFinal = req.Value;
            await _db.SaveChangesAsync();

            return Ok(await BuildChecklistUpdateResponse(ordemId, "controlo", checklistId, req.Value));
        }

        private static bool TipoValido(int tipo)
        {
            return tipo == TIPO_MONTAGEM || tipo == TIPO_EMBALAGEM || tipo == TIPO_CONTROLO;
        }

        private static bool FlagValida(int value)
        {
            return value == 0 || value == 1;
        }

        private static ChecklistSecaoResumoDto BuildResumo(List<int> values)
        {
            var total = values.Count;
            var feitos = values.Count(v => v == 1);

            return new ChecklistSecaoResumoDto
            {
                Total = total,
                Feitos = feitos,
                Inicializado = total > 0,
                Ok = total == 0 || feitos == total
            };
        }

        private async Task<IActionResult?> ValidarAtualizacaoChecklist(int ordemId, int checklistId, int tipoEsperado)
        {
            var ordemExiste = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.IDOrdemProducao == ordemId);

            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var checklist = await _db.Set<Checklist>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IDChecklist == checklistId);

            if (checklist == null)
                return NotFound(new { message = "Checklist não encontrado." });

            if (checklist.Tipo != tipoEsperado)
                return BadRequest(new { message = "O checklist indicado não pertence a esta secção." });

            return null;
        }

        private async Task<object> BuildChecklistUpdateResponse(int ordemId, string secao, int checklistId, int value)
        {
            var all = await GetChecklistExecutionData(ordemId);

            return new
            {
                message = "Checklist atualizado com sucesso.",
                ordemId,
                secao,
                checklistId,
                value,
                resumo = new
                {
                    montagem = all.ResumoMontagem,
                    embalagem = all.ResumoEmbalagem,
                    controlo = all.ResumoControlo,
                    inicializado = all.Inicializado,
                    tudoOk = all.TudoOk
                }
            };
        }

        private async Task<ChecklistExecucaoResponseDto> GetChecklistExecutionData(int ordemId)
        {
            var montagem = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == ordemId)
                .Join(
                    _db.Set<Checklist>().AsNoTracking(),
                    x => x.IDChecklist,
                    c => c.IDChecklist,
                    (x, c) => new ChecklistItemDto
                    {
                        IDChecklist = x.IDChecklist,
                        Nome = c.Nome,
                        Descricao = c.Descricao,
                        Tipo = c.Tipo,
                        Value = x.Verificado
                    })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            var embalagem = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == ordemId)
                .Join(
                    _db.Set<Checklist>().AsNoTracking(),
                    x => x.IDChecklist,
                    c => c.IDChecklist,
                    (x, c) => new ChecklistItemDto
                    {
                        IDChecklist = x.IDChecklist,
                        Nome = c.Nome,
                        Descricao = c.Descricao,
                        Tipo = c.Tipo,
                        Value = x.Incluido
                    })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            var controlo = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == ordemId)
                .Join(
                    _db.Set<Checklist>().AsNoTracking(),
                    x => x.IDChecklist,
                    c => c.IDChecklist,
                    (x, c) => new ChecklistItemDto
                    {
                        IDChecklist = x.IDChecklist,
                        Nome = c.Nome,
                        Descricao = c.Descricao,
                        Tipo = c.Tipo,
                        Value = x.ControloFinal
                    })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            var resumoMontagem = BuildResumo(montagem.Select(x => x.Value).ToList());
            var resumoEmbalagem = BuildResumo(embalagem.Select(x => x.Value).ToList());
            var resumoControlo = BuildResumo(controlo.Select(x => x.Value).ToList());

            return new ChecklistExecucaoResponseDto
            {
                OrdemId = ordemId,
                ResumoMontagem = resumoMontagem,
                ResumoEmbalagem = resumoEmbalagem,
                ResumoControlo = resumoControlo,
                Inicializado = resumoMontagem.Inicializado || resumoEmbalagem.Inicializado || resumoControlo.Inicializado,
                TudoOk = resumoMontagem.Ok && resumoEmbalagem.Ok && resumoControlo.Ok,
                Montagem = montagem,
                Embalagem = embalagem,
                Controlo = controlo
            };
        }
    }
}