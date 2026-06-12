using System.Security.Claims;
using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    [ApiController]
    [Route("api/linha-montagem")]
    public class LinhaMontagemController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_ABERTA = 0;
        private const int ESTADO_EM_PRODUCAO = 1;
        private const int ESTADO_CONCLUIDA = 2;
        private const int ESTADO_BLOQUEADA = 3;

        private const int ESTADO_MOTA_EM_PRODUCAO = 0;
        private const int ESTADO_MOTA_ATIVA = 1;

        private const int CHECKLIST_MONTAGEM = 1;
        private const int CHECKLIST_EMBALAGEM = 2;
        private const int CHECKLIST_CONTROLO = 3;

        private const int ESTADO_ASSOC_ATIVO = 1;

        public LinhaMontagemController(LcmContext db) => _db = db;

        private int? GetUtilizadorId()
        {
            var claim = User.FindFirstValue("utilizador_id");
            return int.TryParse(claim, out var id) ? id : null;
        }

        // GET /api/linha-montagem/minhas-ordens
        // Ordens cujas motas estão atribuídas ao utilizador autenticado
        [HttpGet("minhas-ordens")]
        public async Task<IActionResult> GetMinhasOrdens()
        {
            var utilizadorId = GetUtilizadorId();
            if (utilizadorId == null)
                return BadRequest(new { message = "Utilizador operacional não identificado no token. Verifica o mapeamento em /api/auth/me." });

            var motasIds = await _db.Set<UtilizadorMotum>()
                .AsNoTracking()
                .Where(u => u.IdUtilizador == utilizadorId.Value && u.Estado == ESTADO_ASSOC_ATIVO)
                .Select(u => u.IDMota)
                .ToListAsync();

            if (!motasIds.Any())
                return Ok(new { utilizadorId, total = 0, ordens = Array.Empty<object>() });

            var motas = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(m => motasIds.Contains(m.IDMota) && m.IDOrdemProducao.HasValue)
                .Select(m => new { m.IDMota, m.IDOrdemProducao, m.NumeroIdentificacao, m.Cor, m.IDModelo, m.Estado })
                .ToListAsync();

            var ordemIds = motas.Select(m => m.IDOrdemProducao!.Value).Distinct().ToList();

            var ordens = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .Where(o => ordemIds.Contains(o.IDOrdemProducao))
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.Estado,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    idModelo = o.ModeloMotaIDModelo
                })
                .ToListAsync();

            var modeloIds = ordens.Where(o => o.idModelo.HasValue).Select(o => o.idModelo!.Value).Distinct().ToList();
            var modelosDict = new Dictionary<int, string>();
            if (modeloIds.Any())
            {
                var modelos = await _db.Set<ModelosMotum>().AsNoTracking()
                    .Where(m => modeloIds.Contains(m.IDModelo))
                    .Select(m => new { m.IDModelo, m.Nome })
                    .ToListAsync();
                foreach (var m in modelos) modelosDict[m.IDModelo] = m.Nome;
            }

            var motasPorOrdem = motas
                .Where(m => m.IDOrdemProducao.HasValue)
                .ToDictionary(m => m.IDOrdemProducao!.Value);

            var resultado = ordens.Select(o =>
            {
                var mota = motasPorOrdem.GetValueOrDefault(o.IDOrdemProducao);
                return new
                {
                    ordemId = o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.Estado,
                    estadoNome = GetEstadoOrdemNome(o.Estado),
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    modeloNome = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value) : null,
                    mota = mota == null ? null : new
                    {
                        idMota = mota.IDMota,
                        mota.NumeroIdentificacao,
                        vinPreenchido = !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao),
                        mota.Cor,
                        mota.Estado
                    }
                };
            }).ToList();

            return Ok(new { utilizadorId, total = resultado.Count, ordens = resultado });
        }

        // GET /api/linha-montagem/ordens/{id}
        [HttpGet("ordens/{id:int}")]
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
                    estadoNome = GetEstadoOrdemNome(o.Estado),
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    idModelo = o.ModeloMotaIDModelo
                })
                .FirstOrDefaultAsync();

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(m => m.IDOrdemProducao == id)
                .Select(m => new
                {
                    idMota = m.IDMota,
                    m.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(m.NumeroIdentificacao),
                    m.Cor,
                    m.Quilometragem,
                    m.Estado,
                    m.DataRegisto
                })
                .FirstOrDefaultAsync();

            return Ok(new { ordem, mota });
        }

        // GET /api/linha-montagem/ordens/{id}/checklists
        [HttpGet("ordens/{id:int}/checklists")]
        public async Task<IActionResult> GetChecklists(int id)
        {
            var ordemExiste = await _db.Set<OrdemProducao>().AsNoTracking().AnyAsync(o => o.IDOrdemProducao == id);
            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var montagem = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == id)
                .Join(_db.Set<Checklist>().AsNoTracking(), x => x.IDChecklist, c => c.IDChecklist,
                    (x, c) => new { c.IDChecklist, c.Nome, c.Descricao, value = x.Verificado })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            var embalagem = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == id)
                .Join(_db.Set<Checklist>().AsNoTracking(), x => x.IDChecklist, c => c.IDChecklist,
                    (x, c) => new { c.IDChecklist, c.Nome, c.Descricao, value = x.Incluido })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            return Ok(new
            {
                ordemId = id,
                inicializado = montagem.Any() || embalagem.Any(),
                montagem,
                resumoMontagem = new
                {
                    total = montagem.Count,
                    feitos = montagem.Count(x => x.value == 1),
                    ok = montagem.Count == 0 || montagem.All(x => x.value == 1)
                },
                embalagem,
                resumoEmbalagem = new
                {
                    total = embalagem.Count,
                    feitos = embalagem.Count(x => x.value == 1),
                    ok = embalagem.Count == 0 || embalagem.All(x => x.value == 1)
                }
            });
        }

        // PUT /api/linha-montagem/ordens/{id}/checklists/montagem/{checklistId}
        [HttpPut("ordens/{id:int}/checklists/montagem/{checklistId:int}")]
        public async Task<IActionResult> SetMontagem(int id, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            if (req == null || (req.Value != 0 && req.Value != 1))
                return BadRequest(new { message = "O valor tem de ser 0 ou 1." });

            var ordemExiste = await _db.Set<OrdemProducao>().AsNoTracking().AnyAsync(o => o.IDOrdemProducao == id);
            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var checklist = await _db.Set<Checklist>().AsNoTracking().FirstOrDefaultAsync(c => c.IDChecklist == checklistId);
            if (checklist == null)
                return NotFound(new { message = "Checklist não encontrado." });
            if (checklist.Tipo != CHECKLIST_MONTAGEM)
                return BadRequest(new { message = "O checklist indicado não é de montagem." });

            var row = await _db.Set<ChecklistMontagem>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == id && x.IDChecklist == checklistId);
            if (row == null)
                return BadRequest(new { message = "Checklist de montagem não inicializado. Inicia a ordem primeiro." });

            row.Verificado = req.Value;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Checklist de montagem atualizado.", ordemId = id, checklistId, value = req.Value });
        }

        // PUT /api/linha-montagem/ordens/{id}/checklists/embalagem/{checklistId}
        [HttpPut("ordens/{id:int}/checklists/embalagem/{checklistId:int}")]
        public async Task<IActionResult> SetEmbalagem(int id, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            if (req == null || (req.Value != 0 && req.Value != 1))
                return BadRequest(new { message = "O valor tem de ser 0 ou 1." });

            var ordemExiste = await _db.Set<OrdemProducao>().AsNoTracking().AnyAsync(o => o.IDOrdemProducao == id);
            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var checklist = await _db.Set<Checklist>().AsNoTracking().FirstOrDefaultAsync(c => c.IDChecklist == checklistId);
            if (checklist == null)
                return NotFound(new { message = "Checklist não encontrado." });
            if (checklist.Tipo != CHECKLIST_EMBALAGEM)
                return BadRequest(new { message = "O checklist indicado não é de embalagem." });

            var row = await _db.Set<ChecklistEmbalagem>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == id && x.IDChecklist == checklistId);
            if (row == null)
                return BadRequest(new { message = "Checklist de embalagem não inicializado. Inicia a ordem primeiro." });

            row.Incluido = req.Value;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Checklist de embalagem atualizado.", ordemId = id, checklistId, value = req.Value });
        }

        // POST /api/linha-montagem/ordens/{id}/iniciar
        [HttpPost("ordens/{id:int}/iniciar")]
        public async Task<IActionResult> IniciarOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado == ESTADO_CONCLUIDA)
                return Conflict(new { message = "A ordem já está concluída." });
            if (ordem.Estado == ESTADO_EM_PRODUCAO)
                return Conflict(new { message = "A ordem já está em produção." });

            var modeloId = ordem.ModeloMotaIDModelo ?? 0;
            if (modeloId <= 0)
                return BadRequest(new { message = "A ordem não tem modelo associado." });

            var templates = await _db.Set<ChecklistModelo>()
                .AsNoTracking()
                .Where(cm => cm.IDModelo == modeloId)
                .Join(_db.Set<Checklist>().AsNoTracking(), cm => cm.IDChecklist, c => c.IDChecklist,
                    (cm, c) => new { c.IDChecklist, c.Tipo })
                .Distinct()
                .ToListAsync();

            if (!templates.Any())
                return BadRequest(new { message = "O modelo desta ordem não tem checklists associados." });

            using var tx = await _db.Database.BeginTransactionAsync();

            var montagemSet = (await _db.Set<ChecklistMontagem>().Where(x => x.IDOrdemProducao == id).Select(x => x.IDChecklist).ToListAsync()).ToHashSet();
            var embalagemSet = (await _db.Set<ChecklistEmbalagem>().Where(x => x.IDOrdemProducao == id).Select(x => x.IDChecklist).ToListAsync()).ToHashSet();
            var controloSet = (await _db.Set<ChecklistControlo>().Where(x => x.IDOrdemProducao == id).Select(x => x.IDChecklist).ToListAsync()).ToHashSet();

            foreach (var t in templates)
            {
                switch (t.Tipo)
                {
                    case CHECKLIST_MONTAGEM:
                        if (!montagemSet.Contains(t.IDChecklist))
                            _db.Set<ChecklistMontagem>().Add(new ChecklistMontagem { IDOrdemProducao = id, IDChecklist = t.IDChecklist, Verificado = 0 });
                        break;
                    case CHECKLIST_EMBALAGEM:
                        if (!embalagemSet.Contains(t.IDChecklist))
                            _db.Set<ChecklistEmbalagem>().Add(new ChecklistEmbalagem { IDOrdemProducao = id, IDChecklist = t.IDChecklist, Incluido = 0 });
                        break;
                    case CHECKLIST_CONTROLO:
                        if (!controloSet.Contains(t.IDChecklist))
                            _db.Set<ChecklistControlo>().Add(new ChecklistControlo { IDOrdemProducao = id, IDChecklist = t.IDChecklist, ControloFinal = 0 });
                        break;
                }
            }

            ordem.Estado = ESTADO_EM_PRODUCAO;
            ordem.DataConclusao = null;

            var motaAssociada = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            if (motaAssociada != null)
                motaAssociada.Estado = ESTADO_MOTA_EM_PRODUCAO;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                message = "Ordem iniciada com sucesso.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado)
            });
        }

        // POST /api/linha-montagem/ordens/{id}/motas
        [HttpPost("ordens/{id:int}/motas")]
        public async Task<IActionResult> CriarMota(int id, [FromBody] CriarMotaRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });
            if (req.Quilometragem < 0)
                return BadRequest(new { message = "Quilometragem inválida." });

            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não existe." });

            if (await _db.Set<Mota>().AsNoTracking().AnyAsync(m => m.IDOrdemProducao == id))
                return Conflict(new { message = "Já existe uma mota associada a esta ordem." });

            var modeloDaOrdem = ordem.ModeloMotaIDModelo ?? 0;
            var modeloFinal = req.IDModelo > 0 ? req.IDModelo : modeloDaOrdem;

            if (modeloFinal <= 0)
                return BadRequest(new { message = "IDModelo é obrigatório ou tem de existir na ordem." });

            if (modeloDaOrdem > 0 && req.IDModelo > 0 && req.IDModelo != modeloDaOrdem)
                return BadRequest(new { message = "O IDModelo enviado não corresponde ao modelo da ordem." });

            if (!await _db.Set<ModelosMotum>().AsNoTracking().AnyAsync(m => m.IDModelo == modeloFinal))
                return NotFound(new { message = "Modelo não encontrado." });

            var vin = NormalizeVinOrEmpty(req.NumeroIdentificacao);
            if (!string.IsNullOrWhiteSpace(vin))
            {
                if (await _db.Set<Mota>().AsNoTracking().AnyAsync(m => !string.IsNullOrWhiteSpace(m.NumeroIdentificacao) && m.NumeroIdentificacao!.ToUpper() == vin))
                    return Conflict(new { message = "Já existe uma mota com esse VIN / Número de Identificação." });
            }

            var mota = new Mota
            {
                IDOrdemProducao = id,
                IDModelo = modeloFinal,
                Cor = string.IsNullOrWhiteSpace(req.Cor) ? "N/A" : req.Cor.Trim(),
                Quilometragem = req.Quilometragem,
                Estado = ESTADO_MOTA_EM_PRODUCAO,
                NumeroIdentificacao = vin,
                DataRegisto = DateTime.UtcNow
            };

            _db.Set<Mota>().Add(mota);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Mota registada com sucesso.",
                id = mota.IDMota,
                idMota = mota.IDMota,
                motaId = mota.IDMota,
                idModelo = mota.IDModelo,
                idOrdemProducao = mota.IDOrdemProducao,
                mota.NumeroIdentificacao,
                vinPreenchido = !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao)
            });
        }

        // PUT /api/linha-montagem/motas/{id}/identificacao
        [HttpPut("motas/{id:int}/identificacao")]
        public async Task<IActionResult> RegistarVin(int id, [FromBody] UpdateNumeroIdentificacaoRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.NumeroIdentificacao))
                return BadRequest(new { message = "O VIN / Número de Identificação é obrigatório." });

            var mota = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDMota == id);
            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var vin = req.NumeroIdentificacao.Trim().ToUpper();

            if (await _db.Set<Mota>().AsNoTracking().AnyAsync(m => m.IDMota != id && !string.IsNullOrWhiteSpace(m.NumeroIdentificacao) && m.NumeroIdentificacao!.ToUpper() == vin))
                return Conflict(new { message = "Já existe outra mota com esse VIN / Número de Identificação." });

            mota.NumeroIdentificacao = vin;
            await _db.SaveChangesAsync();

            return Ok(new { message = "VIN registado com sucesso.", idMota = mota.IDMota, mota.NumeroIdentificacao, vinPreenchido = true });
        }

        // GET /api/linha-montagem/motas/{id}/pecas-sn/resumo
        [HttpGet("motas/{id:int}/pecas-sn/resumo")]
        public async Task<IActionResult> GetPecasSnResumo(int id)
        {
            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDMota == id);
            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var obrigatorias = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDModelo == mota.IDModelo)
                .Join(_db.Set<Peca>().AsNoTracking(), x => x.IDPeca, p => p.IDPeca,
                    (x, p) => new { p.IDPeca, p.PartNumber, p.Descricao })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            var registadas = await _db.Set<MotasPecasSN>().AsNoTracking()
                .Where(x => x.IDMota == id).ToListAsync();

            var regMap = registadas.GroupBy(x => x.IDPeca)
                .ToDictionary(g => g.Key, g => g.First().NumeroSerie ?? string.Empty);

            var pecas = obrigatorias.Select(p => new
            {
                p.IDPeca,
                p.PartNumber,
                p.Descricao,
                preenchida = regMap.ContainsKey(p.IDPeca) && !string.IsNullOrWhiteSpace(regMap[p.IDPeca]),
                numeroSerie = regMap.ContainsKey(p.IDPeca) ? regMap[p.IDPeca] : string.Empty
            }).ToList();

            return Ok(new
            {
                idMota = id,
                idModelo = mota.IDModelo,
                totalObrigatorias = pecas.Count,
                preenchidas = pecas.Count(x => x.preenchida),
                ok = pecas.Count == 0 || pecas.All(x => x.preenchida),
                pecas
            });
        }

        // POST /api/linha-montagem/motas/{id}/pecas-sn
        [HttpPost("motas/{id:int}/pecas-sn")]
        public async Task<IActionResult> RegistarPecaSn(int id, [FromBody] AddPecaSnRequest req)
        {
            if (req == null || req.IDPeca <= 0 || string.IsNullOrWhiteSpace(req.NumeroSerie))
                return BadRequest(new { message = "IDPeca e NumeroSerie são obrigatórios." });

            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDMota == id);
            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            if (!await _db.Set<Peca>().AsNoTracking().AnyAsync(p => p.IDPeca == req.IDPeca))
                return NotFound(new { message = "Peça não encontrada." });

            if (!await _db.Set<ModeloPecasSN>().AsNoTracking().AnyAsync(x => x.IDModelo == mota.IDModelo && x.IDPeca == req.IDPeca))
                return BadRequest(new { message = "A peça indicada não é serializada para o modelo desta mota." });

            var sn = req.NumeroSerie.Trim();

            if (await _db.Set<MotasPecasSN>().AsNoTracking().AnyAsync(x => x.IDPeca == req.IDPeca && x.IDMota != id && x.NumeroSerie == sn))
                return Conflict(new { message = "Já existe outra mota com esse número de série para esta peça." });

            var row = await _db.Set<MotasPecasSN>().FirstOrDefaultAsync(x => x.IDMota == id && x.IDPeca == req.IDPeca);
            bool created;
            if (row == null)
            {
                row = new MotasPecasSN { IDMota = id, IDPeca = req.IDPeca, NumeroSerie = sn };
                _db.Set<MotasPecasSN>().Add(row);
                created = true;
            }
            else
            {
                row.NumeroSerie = sn;
                created = false;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = created ? "Número de série registado." : "Número de série atualizado.",
                row.IDMotasPecasSN,
                idPeca = req.IDPeca,
                numeroSerie = sn,
                created
            });
        }

        // GET /api/linha-montagem/ordens/{id}/ficha
        [HttpGet("ordens/{id:int}/ficha")]
        public async Task<IActionResult> GetFicha(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);

            string? modeloNome = null;
            string? modeloCodigo = null;
            if (ordem.ModeloMotaIDModelo.HasValue)
            {
                var modelo = await _db.Set<ModelosMotum>().AsNoTracking()
                    .Where(m => m.IDModelo == ordem.ModeloMotaIDModelo.Value)
                    .Select(m => new { m.Nome, m.CodigoProduto })
                    .FirstOrDefaultAsync();
                modeloNome = modelo?.Nome;
                modeloCodigo = modelo?.CodigoProduto;
            }

            string? clienteNome = null;
            if (ordem.ClienteIDCliente.HasValue)
            {
                clienteNome = await _db.Set<Cliente>().AsNoTracking()
                    .Where(c => c.IDCliente == ordem.ClienteIDCliente.Value)
                    .Select(c => c.Nome)
                    .FirstOrDefaultAsync();
            }

            var modeloIdFicha = ordem.ModeloMotaIDModelo ?? mota?.IDModelo ?? 0;
            int reqMontagem = 0, reqEmbalagem = 0;
            if (modeloIdFicha > 0)
            {
                var templates = await _db.Set<ChecklistModelo>()
                    .AsNoTracking()
                    .Where(cm => cm.IDModelo == modeloIdFicha)
                    .Join(_db.Set<Checklist>().AsNoTracking(), cm => cm.IDChecklist, c => c.IDChecklist,
                        (cm, c) => new { c.IDChecklist, c.Tipo })
                    .Distinct()
                    .ToListAsync();
                reqMontagem = templates.Count(x => x.Tipo == CHECKLIST_MONTAGEM);
                reqEmbalagem = templates.Count(x => x.Tipo == CHECKLIST_EMBALAGEM);
            }

            var montagemTotal = await _db.Set<ChecklistMontagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id);
            var montagemFeitos = await _db.Set<ChecklistMontagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id && x.Verificado == 1);
            var embalagemTotal = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id);
            var embalagemFeitos = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id && x.Incluido == 1);

            int obrigatorias = 0, preenchidas = 0;
            if (mota != null && modeloIdFicha > 0)
            {
                obrigatorias = await _db.Set<ModeloPecasSN>().AsNoTracking().Where(x => x.IDModelo == modeloIdFicha).Select(x => x.IDPeca).Distinct().CountAsync();
                preenchidas = await _db.Set<MotasPecasSN>().AsNoTracking().Where(x => x.IDMota == mota.IDMota && !string.IsNullOrWhiteSpace(x.NumeroSerie)).Select(x => x.IDPeca).Distinct().CountAsync();
            }

            var vinOk = mota != null && !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao);
            var montagemOk = reqMontagem == 0 || (montagemTotal >= reqMontagem && montagemFeitos >= reqMontagem);
            var embalagemOk = reqEmbalagem == 0 || (embalagemTotal >= reqEmbalagem && embalagemFeitos >= reqEmbalagem);
            var pecasSnOk = obrigatorias == 0 || preenchidas >= obrigatorias;

            return Ok(new
            {
                ordemId = id,
                ordem.NumeroOrdem,
                ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                ordem.PaisDestino,
                ordem.DataCriacao,
                ordem.DataConclusao,
                idModelo = ordem.ModeloMotaIDModelo,
                idCliente = ordem.ClienteIDCliente,
                modeloNome,
                modeloCodigo,
                clienteNome,
                mota = mota == null ? null : new
                {
                    idMota = mota.IDMota,
                    mota.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao),
                    mota.Cor,
                    mota.Quilometragem,
                    mota.Estado,
                    mota.DataRegisto
                },
                resumoChecklists = new
                {
                    inicializado = montagemTotal > 0 || embalagemTotal > 0,
                    montagem = new { obrigatorios = reqMontagem, total = montagemTotal, feitos = montagemFeitos, ok = montagemOk },
                    embalagem = new { obrigatorios = reqEmbalagem, total = embalagemTotal, feitos = embalagemFeitos, ok = embalagemOk }
                },
                resumoPecasSn = new { totalObrigatorias = obrigatorias, preenchidas, ok = pecasSnOk },
                vinOk,
                prontaParaControlo = vinOk && montagemOk && embalagemOk && pecasSnOk && mota != null
            });
        }

        // GET /api/linha-montagem/motas/{id}/pecas-sn
        // Alias estável para /pecas-sn/resumo — mesma resposta
        [HttpGet("motas/{id:int}/pecas-sn")]
        public async Task<IActionResult> GetPecasSn(int id)
        {
            return await GetPecasSnResumo(id);
        }

        // GET /api/linha-montagem/modelos/{id}/pecas-sn
        // Peças serializadas obrigatórias do modelo (template, sem dados de mota)
        [HttpGet("modelos/{id:int}/pecas-sn")]
        public async Task<IActionResult> GetModeloPecasSn(int id)
        {
            var modeloExiste = await _db.Set<ModelosMotum>().AsNoTracking().AnyAsync(m => m.IDModelo == id);
            if (!modeloExiste)
                return NotFound(new { message = "Modelo não encontrado." });

            var pecas = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDModelo == id)
                .Join(_db.Set<Peca>().AsNoTracking(), x => x.IDPeca, p => p.IDPeca,
                    (x, p) => new { p.IDPeca, p.PartNumber, p.Descricao, x.EspecificacaoPadrao })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            return Ok(new { idModelo = id, total = pecas.Count, pecas });
        }

        // GET /api/linha-montagem/operadores/{idUtilizador}/motas
        // Motas atribuídas a um operador — devolve lista direta (array)
        [HttpGet("operadores/{idUtilizador:int}/motas")]
        public async Task<IActionResult> GetMotasDoOperador(int idUtilizador, [FromQuery] bool ativasOnly = true)
        {
            var userExiste = await _db.Set<Utilizadore>().AsNoTracking().AnyAsync(u => u.IdUtilizador == idUtilizador);
            if (!userExiste)
                return NotFound(new { message = "Utilizador não encontrado." });

            var query =
                from assoc in _db.Set<UtilizadorMotum>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on assoc.IDMota equals m.IDMota
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where assoc.IdUtilizador == idUtilizador
                select new
                {
                    assoc.IDUtilizadorMota,
                    utilizadorId = assoc.IdUtilizador,
                    motaId = assoc.IDMota,
                    idOrdemProducao = m.IDOrdemProducao,
                    idModelo = m.IDModelo,
                    numeroIdentificacao = m.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(m.NumeroIdentificacao),
                    cor = m.Cor,
                    estadoMota = m.Estado,
                    estadoAssociacao = assoc.Estado,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                };

            if (ativasOnly)
                query = query.Where(x => x.estadoAssociacao == ESTADO_ASSOC_ATIVO);

            var lista = await query.OrderByDescending(x => x.IDUtilizadorMota).ToListAsync();

            return Ok(lista.Select(x => new
            {
                x.IDUtilizadorMota,
                x.utilizadorId,
                x.motaId,
                x.idOrdemProducao,
                x.idModelo,
                x.numeroIdentificacao,
                x.vinPreenchido,
                x.cor,
                x.estadoMota,
                estadoMotaNome = GetEstadoMotaNome(x.estadoMota),
                x.modeloNome,
                x.modeloCodigo
            }).ToList());
        }

        // POST /api/linha-montagem/ordens/{id}/concluir-etapa
        // Valida o estado da linha de montagem SEM alterar o estado da ordem.
        // Devolve prontidão para o controlo de fábrica finalizar.
        [HttpPost("ordens/{id:int}/concluir-etapa")]
        public async Task<IActionResult> ConcluirEtapa(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_EM_PRODUCAO)
                return BadRequest(new { message = $"A ordem tem de estar em produção. Estado atual: {GetEstadoOrdemNome(ordem.Estado)}." });

            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            var modeloId = ordem.ModeloMotaIDModelo ?? mota?.IDModelo ?? 0;

            var pendencias = new List<string>();

            if (mota == null)
            {
                pendencias.Add("Unidade (mota) não registada na ordem.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(mota.NumeroIdentificacao))
                    pendencias.Add("VIN / Número de Identificação em falta.");

                if (modeloId > 0)
                {
                    var templates = await _db.Set<ChecklistModelo>()
                        .AsNoTracking()
                        .Where(cm => cm.IDModelo == modeloId)
                        .Join(_db.Set<Checklist>().AsNoTracking(), cm => cm.IDChecklist, c => c.IDChecklist,
                            (cm, c) => new { c.IDChecklist, c.Tipo })
                        .Distinct()
                        .ToListAsync();

                    var reqMontagem = templates.Count(x => x.Tipo == CHECKLIST_MONTAGEM);
                    var reqEmbalagem = templates.Count(x => x.Tipo == CHECKLIST_EMBALAGEM);

                    if (reqMontagem > 0)
                    {
                        var montagemTotal = await _db.Set<ChecklistMontagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id);
                        var montagemFeitos = await _db.Set<ChecklistMontagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id && x.Verificado == 1);
                        if (montagemTotal < reqMontagem || montagemFeitos < reqMontagem)
                            pendencias.Add($"Checklist de montagem incompleto ({montagemFeitos}/{reqMontagem} itens verificados).");
                    }

                    if (reqEmbalagem > 0)
                    {
                        var embalagemTotal = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id);
                        var embalagemFeitos = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id && x.Incluido == 1);
                        if (embalagemTotal < reqEmbalagem || embalagemFeitos < reqEmbalagem)
                            pendencias.Add($"Checklist de embalagem incompleto ({embalagemFeitos}/{reqEmbalagem} itens verificados).");
                    }

                    var pecasObrigatorias = await _db.Set<ModeloPecasSN>().AsNoTracking()
                        .Where(x => x.IDModelo == modeloId).Select(x => x.IDPeca).Distinct().CountAsync();
                    var pecasPreenchidas = await _db.Set<MotasPecasSN>().AsNoTracking()
                        .Where(x => x.IDMota == mota.IDMota && !string.IsNullOrWhiteSpace(x.NumeroSerie))
                        .Select(x => x.IDPeca).Distinct().CountAsync();

                    if (pecasObrigatorias > 0 && pecasPreenchidas < pecasObrigatorias)
                        pendencias.Add($"Peças serializadas por preencher ({pecasPreenchidas}/{pecasObrigatorias}).");
                }
            }

            if (pendencias.Any())
            {
                return BadRequest(new
                {
                    ordemId = id,
                    prontaParaControlo = false,
                    pendencias
                });
            }

            return Ok(new
            {
                ordemId = id,
                motaId = mota!.IDMota,
                prontaParaControlo = true,
                montagemOk = true,
                embalagemOk = true,
                pecasSnOk = true,
                vinOk = true,
                pendencias = Array.Empty<string>(),
                message = "Etapa de linha de montagem concluída. Ordem pronta para validação pelo controlo de fábrica."
            });
        }

        // POST /api/linha-montagem/ordens/{id}/finalizar
        // Mantido por compatibilidade — redireciona conceptualmente para concluir-etapa.
        // NÃO altera o estado da ordem. Usar /concluir-etapa nas apps novas.
        [HttpPost("ordens/{id:int}/finalizar")]
        public async Task<IActionResult> FinalizarOrdem(int id)
        {
            var result = await ConcluirEtapa(id);
            if (result is OkObjectResult ok)
            {
                return Ok(new
                {
                    aviso = "Este endpoint está deprecated. Usa POST /api/linha-montagem/ordens/{id}/concluir-etapa nas apps novas. A finalização global fica em POST /api/controlo-fabrica/ordens/{id}/finalizar.",
                    resultado = ok.Value
                });
            }
            return result;
        }

        private static string NormalizeVinOrEmpty(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpper();

        private static string GetEstadoOrdemNome(int estado) => estado switch
        {
            ESTADO_ABERTA => "Aberta",
            ESTADO_EM_PRODUCAO => "Em Produção",
            ESTADO_CONCLUIDA => "Concluída",
            ESTADO_BLOQUEADA => "Bloqueada",
            _ => "Desconhecido"
        };

        private static string GetEstadoMotaNome(int estado) => estado switch
        {
            ESTADO_MOTA_EM_PRODUCAO => "Em Produção",
            ESTADO_MOTA_ATIVA => "Ativa",
            2 => "Em Manutenção",
            3 => "Descontinuada",
            _ => "Desconhecido"
        };
    }
}
