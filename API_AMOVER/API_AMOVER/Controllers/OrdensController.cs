using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class CriarOrdemFromEncomendaRequest
    {
        public string NumeroOrdem { get; set; } = string.Empty;
        public string PaisDestino { get; set; } = string.Empty;
        public int Estado { get; set; } = 0; // 0=aberta
    }

    public class UpdateEstadoRequest
    {
        public int Estado { get; set; }
    }

    public class CriarMotaRequest
    {
        public int IDModelo { get; set; }
        public string Cor { get; set; } = "N/A";
        public double Quilometragem { get; set; } = 0;
        public int Estado { get; set; } = 0; // 0=EmProdução
        public string NumeroIdentificacao { get; set; } = "";
    }

    public class ResumoChecklistSecaoDto
    {
        public int Total { get; set; }
        public int Feitos { get; set; }
        public bool Inicializado { get; set; }
        public bool Ok { get; set; }
    }

    public class ResumoChecklistsDto
    {
        public ResumoChecklistSecaoDto Montagem { get; set; } = new();
        public ResumoChecklistSecaoDto Embalagem { get; set; } = new();
        public ResumoChecklistSecaoDto Controlo { get; set; } = new();
        public bool ProntoParaFinalizar { get; set; }
    }

    public class ResumoPecasSnDto
    {
        public int Obrigatorias { get; set; }
        public int Preenchidas { get; set; }
        public bool Ok { get; set; }
    }

    public class ResumoOrdemDto
    {
        public int OrdemId { get; set; }
        public int Motas { get; set; }
        public int Servicos { get; set; }
        public int? MotaId { get; set; }
        public bool TemMotaAssociada { get; set; }
        public bool VinPreenchido { get; set; }
        public ResumoPecasSnDto PecasSn { get; set; } = new();
        public ResumoChecklistsDto Checklists { get; set; } = new();
    }

    public class MotaFichaDto
    {
        public int IDMota { get; set; }
        public string NumeroIdentificacao { get; set; } = string.Empty;
        public bool VinPreenchido { get; set; }
        public string Cor { get; set; } = string.Empty;
        public double Quilometragem { get; set; }
        public int Estado { get; set; }
        public DateTime DataRegisto { get; set; }
    }

    public class ModeloFichaDto
    {
        public int IDModelo { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string CodigoProduto { get; set; } = string.Empty;
        public int Estado { get; set; }
    }

    public class ClienteFichaDto
    {
        public int IDCliente { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int Tipo { get; set; }
    }

    public class EncomendaFichaDto
    {
        public int IDEncomenda { get; set; }
        public int Quantidade { get; set; }
        public int Estado { get; set; }
        public DateTime DateCriacao { get; set; }
        public DateTime? DataEntrega { get; set; }
    }

    public class BloquearOrdemRequest
    {
        public string Motivo { get; set; } = string.Empty;
    }

    public class DesbloquearOrdemRequest
    {
        public string? Resolucao { get; set; }
    }

    [ApiController]
    [Route("api/ordens")]
    public class OrdensController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_ABERTA = 0;
        private const int ESTADO_EM_PRODUCAO = 1;
        private const int ESTADO_CONCLUIDA = 2;
        private const int ESTADO_BLOQUEADA = 3;

        private const int ESTADO_MOTA_EM_PRODUCAO = 0;
        private const int ESTADO_MOTA_ATIVA = 1;
        private const int ESTADO_MOTA_EM_MANUTENCAO = 2;
        private const int ESTADO_MOTA_DESCONTINUADA = 3;

        private const int CHECKLIST_MONTAGEM = 1;
        private const int CHECKLIST_EMBALAGEM = 2;
        private const int CHECKLIST_CONTROLO = 3;

        public OrdensController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/ordens?estado=1&includeNomes=true
        [HttpGet]
        public async Task<IActionResult> GetOrdens(
            [FromQuery] int? estado = null,
            [FromQuery] bool includeNomes = false)
        {
            if (estado.HasValue && !EstadoOrdemValido(estado.Value))
                return BadRequest(new { message = "Estado inválido para ordem." });

            var query = _db.Set<OrdemProducao>().AsNoTracking();

            if (estado.HasValue)
                query = query.Where(o => o.Estado == estado.Value);

            var ordens = await query
                .OrderByDescending(o => o.DataCriacao)
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.Estado,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    o.IDEncomenda,
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .ToListAsync();

            if (!includeNomes)
                return Ok(ordens);

            var clienteIds = ordens
                .Where(o => o.idCliente.HasValue)
                .Select(o => o.idCliente!.Value)
                .Distinct()
                .ToList();

            var modeloIds = ordens
                .Where(o => o.idModelo.HasValue)
                .Select(o => o.idModelo!.Value)
                .Distinct()
                .ToList();

            var clientesDict = new Dictionary<int, string>();
            if (clienteIds.Count > 0)
            {
                var cl = await _db.Set<Cliente>().AsNoTracking()
                    .Where(c => clienteIds.Contains(c.IDCliente))
                    .Select(c => new { c.IDCliente, c.Nome })
                    .ToListAsync();
                foreach (var c in cl) clientesDict[c.IDCliente] = c.Nome;
            }

            var modelosDict = new Dictionary<int, (string Nome, string CodigoProduto)>();
            if (modeloIds.Count > 0)
            {
                var ml = await _db.Set<ModelosMotum>().AsNoTracking()
                    .Where(m => modeloIds.Contains(m.IDModelo))
                    .Select(m => new { m.IDModelo, m.Nome, m.CodigoProduto })
                    .ToListAsync();
                foreach (var m in ml) modelosDict[m.IDModelo] = (m.Nome, m.CodigoProduto);
            }

            return Ok(ordens.Select(o => new
            {
                o.IDOrdemProducao,
                o.NumeroOrdem,
                o.Estado,
                o.PaisDestino,
                o.DataCriacao,
                o.DataConclusao,
                o.IDEncomenda,
                idModelo = o.idModelo,
                idCliente = o.idCliente,
                clienteNome = o.idCliente.HasValue ? clientesDict.GetValueOrDefault(o.idCliente.Value) : null,
                modeloNome = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).Nome : null,
                modeloCodigo = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).CodigoProduto : null
            }));
        }

        // GET /api/ordens/{id}
        [HttpGet("{id:int}")]
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
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    o.IDEncomenda,
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .FirstOrDefaultAsync();

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            return Ok(ordem);
        }

        // POST /api/ordens/from-encomenda/{encomendaId}
        [HttpPost("from-encomenda/{encomendaId:int}")]
        public async Task<IActionResult> CriarFromEncomenda(int encomendaId, [FromBody] CriarOrdemFromEncomendaRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.NumeroOrdem) || string.IsNullOrWhiteSpace(req.PaisDestino))
                return BadRequest(new { message = "NumeroOrdem e PaisDestino são obrigatórios." });

            if (req.Estado != ESTADO_ABERTA)
                return BadRequest(new { message = "Uma nova ordem criada a partir da encomenda deve ficar em estado Aberta." });

            var encomenda = await _db.Set<Encomenda>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.IDEncomenda == encomendaId);

            if (encomenda == null)
                return NotFound(new { message = "Encomenda não encontrada." });

            var totalOrdensDaEncomenda = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .CountAsync(o => o.IDEncomenda == encomendaId);

            if (encomenda.Quantidade > 0 && totalOrdensDaEncomenda >= encomenda.Quantidade)
            {
                return Conflict(new
                {
                    message = "Já foram criadas todas as ordens previstas para esta encomenda."
                });
            }

            var numeroOrdem = req.NumeroOrdem.Trim();

            var numeroJaExiste = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.NumeroOrdem == numeroOrdem);

            if (numeroJaExiste)
                return Conflict(new { message = "Já existe uma ordem com esse Número de Ordem." });

            var ordem = new OrdemProducao
            {
                IDEncomenda = encomendaId,
                NumeroOrdem = numeroOrdem,
                PaisDestino = req.PaisDestino.Trim(),
                Estado = ESTADO_ABERTA,
                DataCriacao = DateTime.UtcNow,
                ClienteIDCliente = encomenda.IDCliente,
                ModeloMotaIDModelo = encomenda.IDModelo
            };

            _db.Set<OrdemProducao>().Add(ordem);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrdem), new { id = ordem.IDOrdemProducao }, new { ordem.IDOrdemProducao });
        }

        // POST /api/ordens/{id}/iniciar
        [HttpPost("{id:int}/iniciar")]
        public async Task<IActionResult> IniciarOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado == ESTADO_CONCLUIDA)
                return Conflict(new { message = "A ordem já está concluída e não pode ser iniciada novamente." });

            if (ordem.Estado == ESTADO_EM_PRODUCAO)
                return Conflict(new { message = "A ordem já está em produção." });

            var modeloId = ordem.ModeloMotaIDModelo ?? 0;
            if (modeloId <= 0)
                return BadRequest(new { message = "A ordem não tem modelo associado. Não é possível iniciar." });

            var templates = await _db.Set<ChecklistModelo>()
                .AsNoTracking()
                .Where(cm => cm.IDModelo == modeloId)
                .Join(
                    _db.Set<Checklist>().AsNoTracking(),
                    cm => cm.IDChecklist,
                    c => c.IDChecklist,
                    (cm, c) => new
                    {
                        c.IDChecklist,
                        c.Tipo
                    })
                .Distinct()
                .ToListAsync();

            if (!templates.Any())
                return BadRequest(new { message = "O modelo desta ordem não tem checklists associados." });

            using var tx = await _db.Database.BeginTransactionAsync();

            var montagemExistentes = await _db.Set<ChecklistMontagem>()
                .Where(x => x.IDOrdemProducao == id)
                .Select(x => x.IDChecklist)
                .ToListAsync();

            var embalagemExistentes = await _db.Set<ChecklistEmbalagem>()
                .Where(x => x.IDOrdemProducao == id)
                .Select(x => x.IDChecklist)
                .ToListAsync();

            var controloExistentes = await _db.Set<ChecklistControlo>()
                .Where(x => x.IDOrdemProducao == id)
                .Select(x => x.IDChecklist)
                .ToListAsync();

            var montagemSet = montagemExistentes.ToHashSet();
            var embalagemSet = embalagemExistentes.ToHashSet();
            var controloSet = controloExistentes.ToHashSet();

            int criadosMontagem = 0;
            int criadosEmbalagem = 0;
            int criadosControlo = 0;

            foreach (var t in templates)
            {
                switch (t.Tipo)
                {
                    case CHECKLIST_MONTAGEM:
                        if (!montagemSet.Contains(t.IDChecklist))
                        {
                            _db.Set<ChecklistMontagem>().Add(new ChecklistMontagem
                            {
                                IDOrdemProducao = id,
                                IDChecklist = t.IDChecklist,
                                Verificado = 0
                            });
                            criadosMontagem++;
                        }
                        break;

                    case CHECKLIST_EMBALAGEM:
                        if (!embalagemSet.Contains(t.IDChecklist))
                        {
                            _db.Set<ChecklistEmbalagem>().Add(new ChecklistEmbalagem
                            {
                                IDOrdemProducao = id,
                                IDChecklist = t.IDChecklist,
                                Incluido = 0
                            });
                            criadosEmbalagem++;
                        }
                        break;

                    case CHECKLIST_CONTROLO:
                        if (!controloSet.Contains(t.IDChecklist))
                        {
                            _db.Set<ChecklistControlo>().Add(new ChecklistControlo
                            {
                                IDOrdemProducao = id,
                                IDChecklist = t.IDChecklist,
                                ControloFinal = 0
                            });
                            criadosControlo++;
                        }
                        break;
                }
            }

            ordem.Estado = ESTADO_EM_PRODUCAO;
            ordem.DataConclusao = null;

            var motaAssociada = await _db.Set<Mota>()
                .FirstOrDefaultAsync(m => m.IDOrdemProducao == id);

            if (motaAssociada != null)
                motaAssociada.Estado = ESTADO_MOTA_EM_PRODUCAO;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var resumo = await BuildResumoAsync(id);

            return Ok(new
            {
                message = "Ordem iniciada com sucesso.",
                ordemId = id,
                estado = ordem.Estado,
                checklistsCriados = new
                {
                    montagem = criadosMontagem,
                    embalagem = criadosEmbalagem,
                    controlo = criadosControlo
                },
                resumo
            });
        }

        // POST /api/ordens/{id}/finalizar
        [HttpPost("{id:int}/finalizar")]
        public async Task<IActionResult> FinalizarOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado == ESTADO_CONCLUIDA)
                return Conflict(new { message = "A ordem já está concluída." });

            if (ordem.Estado != ESTADO_EM_PRODUCAO)
                return BadRequest(new { message = "A ordem tem de estar em produção antes de ser finalizada." });

            var mota = await _db.Set<Mota>()
                .FirstOrDefaultAsync(m => m.IDOrdemProducao == id);

            if (mota == null)
                return BadRequest(new { message = "A ordem ainda não tem mota associada." });

            if (string.IsNullOrWhiteSpace(mota.NumeroIdentificacao))
                return BadRequest(new { message = "A mota ainda não tem Número de Identificação / quadro registado." });

            var modeloId = ordem.ModeloMotaIDModelo ?? mota.IDModelo;
            if (modeloId <= 0)
                return BadRequest(new { message = "Não foi possível determinar o modelo da ordem." });

            var templates = await _db.Set<ChecklistModelo>()
                .AsNoTracking()
                .Where(cm => cm.IDModelo == modeloId)
                .Join(
                    _db.Set<Checklist>().AsNoTracking(),
                    cm => cm.IDChecklist,
                    c => c.IDChecklist,
                    (cm, c) => new
                    {
                        c.IDChecklist,
                        c.Tipo
                    })
                .Distinct()
                .ToListAsync();

            var reqMontagem = templates.Count(x => x.Tipo == CHECKLIST_MONTAGEM);
            var reqEmbalagem = templates.Count(x => x.Tipo == CHECKLIST_EMBALAGEM);
            var reqControlo = templates.Count(x => x.Tipo == CHECKLIST_CONTROLO);

            var montagemTotal = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == id);

            var montagemFeitos = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == id && x.Verificado == 1);

            var embalagemTotal = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == id);

            var embalagemFeitos = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == id && x.Incluido == 1);

            var controloTotal = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == id);

            var controloFeitos = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == id && x.ControloFinal == 1);

            if (reqMontagem > 0 && (montagemTotal < reqMontagem || montagemFeitos < reqMontagem))
                return BadRequest(new { message = "Checklist de montagem incompleto." });

            if (reqEmbalagem > 0 && (embalagemTotal < reqEmbalagem || embalagemFeitos < reqEmbalagem))
                return BadRequest(new { message = "Checklist de embalagem incompleto." });

            if (reqControlo > 0 && (controloTotal < reqControlo || controloFeitos < reqControlo))
                return BadRequest(new { message = "Checklist de controlo final incompleto." });

            var pecasObrigatoriasIds = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDModelo == modeloId)
                .Select(x => x.IDPeca)
                .Distinct()
                .ToListAsync();

            var pecasPreenchidasIds = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDMota == mota.IDMota && !string.IsNullOrWhiteSpace(x.NumeroSerie))
                .Select(x => x.IDPeca)
                .Distinct()
                .ToListAsync();

            var pecasEmFaltaIds = pecasObrigatoriasIds.Except(pecasPreenchidasIds).ToList();

            if (pecasEmFaltaIds.Any())
            {
                var pecasEmFalta = await _db.Set<Peca>()
                    .AsNoTracking()
                    .Where(p => pecasEmFaltaIds.Contains(p.IDPeca))
                    .Select(p => new
                    {
                        p.IDPeca,
                        p.PartNumber,
                        p.Descricao
                    })
                    .ToListAsync();

                return BadRequest(new
                {
                    message = "Existem peças serializadas obrigatórias por preencher.",
                    pecasEmFalta
                });
            }

            ordem.Estado = ESTADO_CONCLUIDA;
            ordem.DataConclusao = DateTime.UtcNow;
            mota.Estado = ESTADO_MOTA_ATIVA;

            await _db.SaveChangesAsync();

            var resumo = await BuildResumoAsync(id);

            return Ok(new
            {
                message = "Ordem finalizada com sucesso.",
                ordemId = id,
                estado = ordem.Estado,
                dataConclusao = ordem.DataConclusao,
                motaEstado = mota.Estado,
                resumo
            });
        }

        // PUT /api/ordens/{id}/estado
        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> UpdateEstado(int id, [FromBody] UpdateEstadoRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (!EstadoOrdemValido(req.Estado))
                return BadRequest(new { message = "Estado inválido para ordem." });

            var ordem = await _db.Set<OrdemProducao>()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (req.Estado == ESTADO_EM_PRODUCAO)
                return BadRequest(new { message = "Para iniciar a ordem usa POST /api/ordens/{id}/iniciar." });

            if (req.Estado == ESTADO_CONCLUIDA)
                return BadRequest(new { message = "Para concluir a ordem usa POST /api/ordens/{id}/finalizar." });

            ordem.Estado = ESTADO_ABERTA;
            ordem.DataConclusao = null;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // GET /api/ordens/{id}/resumo
        [HttpGet("{id:int}/resumo")]
        public async Task<IActionResult> Resumo(int id)
        {
            var ordemExists = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.IDOrdemProducao == id);

            if (!ordemExists)
                return NotFound(new { message = "Ordem não encontrada." });

            var resumo = await BuildResumoAsync(id);
            return Ok(resumo);
        }

        // GET /api/ordens/{id}/motas
        [HttpGet("{id:int}/motas")]
        public async Task<IActionResult> GetMotasDaOrdem(int id)
        {
            var ordemExiste = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.IDOrdemProducao == id);

            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var lista = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(m => m.IDOrdemProducao == id)
                .OrderByDescending(m => m.DataRegisto)
                .Select(m => new
                {
                    m.IDMota,
                    m.IDModelo,
                    m.IDOrdemProducao,
                    m.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(m.NumeroIdentificacao),
                    m.Cor,
                    m.Quilometragem,
                    m.Estado,
                    m.DataRegisto
                })
                .ToListAsync();

            return Ok(lista);
        }

        // POST /api/ordens/{id}/motas
        [HttpPost("{id:int}/motas")]
        public async Task<IActionResult> CriarMotaNaOrdem(int id, [FromBody] CriarMotaRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.Quilometragem < 0)
                return BadRequest(new { message = "Quilometragem inválida." });

            var ordem = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não existe." });

            var jaExisteMota = await _db.Set<Mota>()
                .AsNoTracking()
                .AnyAsync(m => m.IDOrdemProducao == id);

            if (jaExisteMota)
                return Conflict(new { message = "Já existe uma mota associada a esta ordem." });

            var modeloDaOrdem = ordem.ModeloMotaIDModelo ?? 0;
            var modeloFinal = req.IDModelo > 0 ? req.IDModelo : modeloDaOrdem;

            if (modeloFinal <= 0)
                return BadRequest(new { message = "IDModelo é obrigatório ou tem de existir na ordem." });

            if (modeloDaOrdem > 0 && req.IDModelo > 0 && req.IDModelo != modeloDaOrdem)
                return BadRequest(new { message = "O IDModelo enviado não corresponde ao modelo da ordem." });

            var modeloExiste = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .AnyAsync(m => m.IDModelo == modeloFinal);

            if (!modeloExiste)
                return NotFound(new { message = "Modelo não encontrado." });

            var numeroIdentificacao = NormalizeNumeroIdentificacaoOrEmpty(req.NumeroIdentificacao);

            if (!string.IsNullOrWhiteSpace(numeroIdentificacao))
            {
                var vinDuplicado = await _db.Set<Mota>()
                    .AsNoTracking()
                    .AnyAsync(m =>
                        !string.IsNullOrWhiteSpace(m.NumeroIdentificacao) &&
                        m.NumeroIdentificacao!.ToUpper() == numeroIdentificacao);

                if (vinDuplicado)
                    return Conflict(new { message = "Já existe uma mota com esse VIN / Número de Identificação." });
            }

            var mota = new Mota
            {
                IDOrdemProducao = id,
                IDModelo = modeloFinal,
                Cor = string.IsNullOrWhiteSpace(req.Cor) ? "N/A" : req.Cor.Trim(),
                Quilometragem = req.Quilometragem,
                Estado = ESTADO_MOTA_EM_PRODUCAO,
                NumeroIdentificacao = numeroIdentificacao,
                DataRegisto = DateTime.UtcNow
            };

            _db.Set<Mota>().Add(mota);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                mota.IDMota,
                mota.IDModelo,
                mota.IDOrdemProducao,
                mota.NumeroIdentificacao,
                vinPreenchido = !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao)
            });
        }

        // GET /api/ordens/prontos-expedicao
        [HttpGet("prontos-expedicao")]
        public async Task<IActionResult> GetProntosExpedicao()
        {
            var ordens = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .Where(o => o.Estado == ESTADO_CONCLUIDA)
                .OrderByDescending(o => o.DataConclusao)
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    o.IDEncomenda,
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .ToListAsync();

            if (ordens.Count == 0)
                return Ok(new { total = 0, ordens = new List<object>() });

            var ordemIds = ordens.Select(o => o.IDOrdemProducao).ToList();
            var clienteIds = ordens.Where(o => o.idCliente.HasValue).Select(o => o.idCliente!.Value).Distinct().ToList();
            var modeloIds = ordens.Where(o => o.idModelo.HasValue).Select(o => o.idModelo!.Value).Distinct().ToList();

            var motas = await _db.Set<Mota>().AsNoTracking()
                .Where(m => ordemIds.Contains(m.IDOrdemProducao))
                .Select(m => new { m.IDMota, m.IDOrdemProducao, m.NumeroIdentificacao, m.Cor, m.Quilometragem })
                .ToListAsync();

            var clientesDict = new Dictionary<int, string>();
            if (clienteIds.Count > 0)
            {
                var cl = await _db.Set<Cliente>().AsNoTracking()
                    .Where(c => clienteIds.Contains(c.IDCliente))
                    .Select(c => new { c.IDCliente, c.Nome })
                    .ToListAsync();
                foreach (var c in cl) clientesDict[c.IDCliente] = c.Nome;
            }

            var modelosDict = new Dictionary<int, (string Nome, string CodigoProduto)>();
            if (modeloIds.Count > 0)
            {
                var ml = await _db.Set<ModelosMotum>().AsNoTracking()
                    .Where(m => modeloIds.Contains(m.IDModelo))
                    .Select(m => new { m.IDModelo, m.Nome, m.CodigoProduto })
                    .ToListAsync();
                foreach (var m in ml) modelosDict[m.IDModelo] = (m.Nome, m.CodigoProduto);
            }

            var motasDict = motas.ToDictionary(m => m.IDOrdemProducao);

            var resultado = ordens.Select(o =>
            {
                var mota = motasDict.GetValueOrDefault(o.IDOrdemProducao);
                return new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    idModelo = o.idModelo,
                    idCliente = o.idCliente,
                    clienteNome = o.idCliente.HasValue ? clientesDict.GetValueOrDefault(o.idCliente.Value) : null,
                    modeloNome = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).Nome : null,
                    modeloCodigo = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).CodigoProduto : null,
                    motaId = mota?.IDMota,
                    vin = mota?.NumeroIdentificacao,
                    vinPreenchido = mota != null && !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao)
                };
            }).ToList();

            return Ok(new { total = resultado.Count, ordens = resultado });
        }

        // POST /api/ordens/{id}/bloquear
        [HttpPost("{id:int}/bloquear")]
        public async Task<IActionResult> BloquearOrdem(int id, [FromBody] BloquearOrdemRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.Motivo))
                return BadRequest(new { message = "O motivo do bloqueio é obrigatório." });

            var ordem = await _db.Set<OrdemProducao>()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado == ESTADO_BLOQUEADA)
                return Conflict(new { message = "A ordem já está bloqueada." });

            if (ordem.Estado == ESTADO_CONCLUIDA)
                return Conflict(new { message = "Uma ordem concluída não pode ser bloqueada." });

            ordem.Estado = ESTADO_BLOQUEADA;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Ordem bloqueada com sucesso.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                motivo = string.IsNullOrWhiteSpace(req.Motivo) ? null : req.Motivo.Trim(),
                aviso = "O motivo de bloqueio foi registado mas não é persistido. A BD atual não tem campo para o efeito."
            });
        }

        // POST /api/ordens/{id}/desbloquear
        [HttpPost("{id:int}/desbloquear")]
        public async Task<IActionResult> DesbloquearOrdem(int id, [FromBody] DesbloquearOrdemRequest? req = null)
        {
            var ordem = await _db.Set<OrdemProducao>()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_BLOQUEADA)
                return Conflict(new { message = "A ordem não está bloqueada." });

            // Heurística: se já tem checklists inicializados, estava em produção antes do bloqueio
            var temChecklists = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .AnyAsync(x => x.IDOrdemProducao == id);

            ordem.Estado = temChecklists ? ESTADO_EM_PRODUCAO : ESTADO_ABERTA;
            await _db.SaveChangesAsync();

            var resumo = await BuildResumoAsync(id);

            var resolucaoTrimmed = string.IsNullOrWhiteSpace(req?.Resolucao) ? null : req.Resolucao.Trim();

            return Ok(new
            {
                message = $"Ordem desbloqueada com sucesso. Estado reposto para '{GetEstadoOrdemNome(ordem.Estado)}'.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                resolucao = resolucaoTrimmed,
                aviso = resolucaoTrimmed != null ? "A resolução foi registada mas não é persistida. A BD atual não tem campo para o efeito." : null,
                resumo
            });
        }

        // GET /api/ordens/{id}/ficha
        [HttpGet("{id:int}/ficha")]
        public async Task<IActionResult> GetFicha(int id)
        {
            var ordemExiste = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.IDOrdemProducao == id);

            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var ordem = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .FirstAsync(o => o.IDOrdemProducao == id);

            ModeloFichaDto? modelo = null;
            if (ordem.ModeloMotaIDModelo.HasValue)
            {
                modelo = await _db.Set<ModelosMotum>()
                    .AsNoTracking()
                    .Where(m => m.IDModelo == ordem.ModeloMotaIDModelo.Value)
                    .Select(m => new ModeloFichaDto
                    {
                        IDModelo = m.IDModelo,
                        Nome = m.Nome,
                        CodigoProduto = m.CodigoProduto,
                        Estado = m.Estado
                    })
                    .FirstOrDefaultAsync();
            }

            ClienteFichaDto? cliente = null;
            if (ordem.ClienteIDCliente.HasValue)
            {
                cliente = await _db.Set<Cliente>()
                    .AsNoTracking()
                    .Where(c => c.IDCliente == ordem.ClienteIDCliente.Value)
                    .Select(c => new ClienteFichaDto
                    {
                        IDCliente = c.IDCliente,
                        Nome = c.Nome,
                        Tipo = c.Tipo
                    })
                    .FirstOrDefaultAsync();
            }

            var encomenda = await _db.Set<Encomenda>()
                .AsNoTracking()
                .Where(e => e.IDEncomenda == ordem.IDEncomenda)
                .Select(e => new EncomendaFichaDto
                {
                    IDEncomenda = e.IDEncomenda,
                    Quantidade = e.Quantidade,
                    Estado = e.Estado,
                    DateCriacao = e.DateCriacao,
                    DataEntrega = e.DataEntrega
                })
                .FirstOrDefaultAsync();

            var motaRaw = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDOrdemProducao == id);

            var motaFicha = motaRaw == null ? null : new MotaFichaDto
            {
                IDMota = motaRaw.IDMota,
                NumeroIdentificacao = motaRaw.NumeroIdentificacao ?? string.Empty,
                VinPreenchido = !string.IsNullOrWhiteSpace(motaRaw.NumeroIdentificacao),
                Cor = motaRaw.Cor ?? string.Empty,
                Quilometragem = motaRaw.Quilometragem,
                Estado = motaRaw.Estado,
                DataRegisto = motaRaw.DataRegisto
            };

            var resumo = await BuildResumoAsync(id);

            var utilizadoresAtivos = 0;
            if (motaRaw != null)
            {
                utilizadoresAtivos = await _db.Set<UtilizadorMotum>()
                    .AsNoTracking()
                    .CountAsync(x => x.IDMota == motaRaw.IDMota && x.Estado == 1);
            }

            return Ok(new
            {
                ordemId = ordem.IDOrdemProducao,
                ordem.NumeroOrdem,
                ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                ordem.PaisDestino,
                ordem.DataCriacao,
                ordem.DataConclusao,
                encomenda,
                cliente,
                modelo,
                mota = motaFicha,
                resumo.TemMotaAssociada,
                resumo.VinPreenchido,
                checklists = resumo.Checklists,
                pecasSn = resumo.PecasSn,
                utilizadoresAtivos,
                servicos = resumo.Servicos,
                prontoParaFinalizar = resumo.Checklists.ProntoParaFinalizar
            });
        }

        // POST /api/ordens/{id}/reabrir
        [HttpPost("{id:int}/reabrir")]
        public async Task<IActionResult> ReabrirOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == id);

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_CONCLUIDA)
                return Conflict(new { message = "Apenas ordens concluídas podem ser reabertas." });

            var mota = await _db.Set<Mota>()
                .FirstOrDefaultAsync(m => m.IDOrdemProducao == id);

            ordem.Estado = ESTADO_EM_PRODUCAO;
            ordem.DataConclusao = null;

            if (mota != null && mota.Estado == ESTADO_MOTA_ATIVA)
                mota.Estado = ESTADO_MOTA_EM_PRODUCAO;

            await _db.SaveChangesAsync();

            var resumo = await BuildResumoAsync(id);

            return Ok(new
            {
                message = "Ordem reaberta com sucesso.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                resumo
            });
        }

        // GET /api/ordens/{id}/utilizadores?ativasOnly=true
        [HttpGet("{id:int}/utilizadores")]
        public async Task<IActionResult> GetUtilizadoresDaOrdem(int id, [FromQuery] bool ativasOnly = true)
        {
            var ordemExiste = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .AnyAsync(o => o.IDOrdemProducao == id);

            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDOrdemProducao == id);

            if (mota == null)
                return Ok(new { ordemId = id, motaId = (int?)null, total = 0, utilizadores = new List<object>() });

            var query =
                from um in _db.Set<UtilizadorMotum>().AsNoTracking()
                join u in _db.Set<Utilizadore>().AsNoTracking() on um.IdUtilizador equals u.IdUtilizador
                where um.IDMota == mota.IDMota
                select new
                {
                    um.IDUtilizadorMota,
                    u.IdUtilizador,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    estadoAssociacao = um.Estado,
                    um.DataCriacao,
                    um.DataInativacao
                };

            if (ativasOnly)
                query = query.Where(x => x.estadoAssociacao == 1);

            var utilizadores = await query
                .OrderBy(x => x.Nome)
                .ToListAsync();

            return Ok(new
            {
                ordemId = id,
                motaId = mota.IDMota,
                ativasOnly,
                total = utilizadores.Count,
                utilizadores
            });
        }

        private async Task<ResumoOrdemDto> BuildResumoAsync(int ordemId)
        {
            var ordem = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .FirstAsync(o => o.IDOrdemProducao == ordemId);

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDOrdemProducao == ordemId);

            var motas = mota == null ? 0 : 1;

            var servicos = mota == null
                ? 0
                : await _db.Set<Servico>()
                    .AsNoTracking()
                    .CountAsync(s => s.IDMota == mota.IDMota);

            var modeloId = ordem.ModeloMotaIDModelo ?? mota?.IDModelo ?? 0;

            int reqMontagem = 0;
            int reqEmbalagem = 0;
            int reqControlo = 0;

            if (modeloId > 0)
            {
                var templates = await _db.Set<ChecklistModelo>()
                    .AsNoTracking()
                    .Where(cm => cm.IDModelo == modeloId)
                    .Join(
                        _db.Set<Checklist>().AsNoTracking(),
                        cm => cm.IDChecklist,
                        c => c.IDChecklist,
                        (cm, c) => new { c.IDChecklist, c.Tipo })
                    .Distinct()
                    .ToListAsync();

                reqMontagem = templates.Count(x => x.Tipo == CHECKLIST_MONTAGEM);
                reqEmbalagem = templates.Count(x => x.Tipo == CHECKLIST_EMBALAGEM);
                reqControlo = templates.Count(x => x.Tipo == CHECKLIST_CONTROLO);
            }

            var montagemTotal = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == ordemId);

            var montagemFeitos = await _db.Set<ChecklistMontagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == ordemId && x.Verificado == 1);

            var embalagemTotal = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == ordemId);

            var embalagemFeitos = await _db.Set<ChecklistEmbalagem>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == ordemId && x.Incluido == 1);

            var controloTotal = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == ordemId);

            var controloFeitos = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .CountAsync(x => x.IDOrdemProducao == ordemId && x.ControloFinal == 1);

            int obrigatorias = 0;
            int preenchidas = 0;

            if (modeloId > 0 && mota != null)
            {
                obrigatorias = await _db.Set<ModeloPecasSN>()
                    .AsNoTracking()
                    .Where(x => x.IDModelo == modeloId)
                    .Select(x => x.IDPeca)
                    .Distinct()
                    .CountAsync();

                preenchidas = await _db.Set<MotasPecasSN>()
                    .AsNoTracking()
                    .Where(x => x.IDMota == mota.IDMota && !string.IsNullOrWhiteSpace(x.NumeroSerie))
                    .Select(x => x.IDPeca)
                    .Distinct()
                    .CountAsync();
            }

            var resumo = new ResumoOrdemDto
            {
                OrdemId = ordemId,
                Motas = motas,
                Servicos = servicos,
                MotaId = mota?.IDMota,
                TemMotaAssociada = mota != null,
                VinPreenchido = mota != null && !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao),
                PecasSn = new ResumoPecasSnDto
                {
                    Obrigatorias = obrigatorias,
                    Preenchidas = preenchidas,
                    Ok = obrigatorias == 0 || preenchidas >= obrigatorias
                },
                Checklists = new ResumoChecklistsDto
                {
                    Montagem = new ResumoChecklistSecaoDto
                    {
                        Total = montagemTotal,
                        Feitos = montagemFeitos,
                        Inicializado = montagemTotal > 0,
                        Ok = reqMontagem == 0 || (montagemTotal >= reqMontagem && montagemFeitos >= reqMontagem)
                    },
                    Embalagem = new ResumoChecklistSecaoDto
                    {
                        Total = embalagemTotal,
                        Feitos = embalagemFeitos,
                        Inicializado = embalagemTotal > 0,
                        Ok = reqEmbalagem == 0 || (embalagemTotal >= reqEmbalagem && embalagemFeitos >= reqEmbalagem)
                    },
                    Controlo = new ResumoChecklistSecaoDto
                    {
                        Total = controloTotal,
                        Feitos = controloFeitos,
                        Inicializado = controloTotal > 0,
                        Ok = reqControlo == 0 || (controloTotal >= reqControlo && controloFeitos >= reqControlo)
                    }
                }
            };

            resumo.Checklists.ProntoParaFinalizar =
                ordem.Estado == ESTADO_EM_PRODUCAO &&
                resumo.Checklists.Montagem.Ok &&
                resumo.Checklists.Embalagem.Ok &&
                resumo.Checklists.Controlo.Ok &&
                resumo.TemMotaAssociada &&
                resumo.VinPreenchido &&
                resumo.PecasSn.Ok;

            return resumo;
        }

        private static bool EstadoOrdemValido(int estado)
        {
            return estado == ESTADO_ABERTA ||
                   estado == ESTADO_EM_PRODUCAO ||
                   estado == ESTADO_CONCLUIDA ||
                   estado == ESTADO_BLOQUEADA;
        }

        private static bool EstadoMotaValido(int estado)
        {
            return estado == ESTADO_MOTA_EM_PRODUCAO ||
                   estado == ESTADO_MOTA_ATIVA ||
                   estado == ESTADO_MOTA_EM_MANUTENCAO ||
                   estado == ESTADO_MOTA_DESCONTINUADA;
        }

        private static string NormalizeNumeroIdentificacao(string value)
        {
            return (value ?? string.Empty).Trim().ToUpper();
        }

        private static string NormalizeNumeroIdentificacaoOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeNumeroIdentificacao(value);
        }

        private static string GetEstadoOrdemNome(int estado) => estado switch
        {
            ESTADO_ABERTA => "Aberta",
            ESTADO_EM_PRODUCAO => "Em Produção",
            ESTADO_CONCLUIDA => "Concluída",
            ESTADO_BLOQUEADA => "Bloqueada",
            _ => "Desconhecido"
        };
    }
}