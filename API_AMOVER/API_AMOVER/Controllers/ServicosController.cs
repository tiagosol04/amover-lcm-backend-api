using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class CreateServicoRequest
    {
        public int IDMota { get; set; }
        public int Tipo { get; set; }
        public string? Descricao { get; set; }
        public int Estado { get; set; } = 0; // 0=Agendado, 1=EmCurso, 2=Concluido
        public DateTime? DataServico { get; set; }
        public DateTime? DataConclusao { get; set; }
        public string? NotasServico { get; set; }
    }

    public class UpdateServicoEstadoRequest
    {
        public int Estado { get; set; }
        public DateTime? DataConclusao { get; set; }
    }

    public class AddPecaAlteradaRequest
    {
        public int IDMotasPecasSN { get; set; }
        public string? Observacoes { get; set; }
        public string? NovoNumeroSerie { get; set; }
    }

    [ApiController]
    [Route("api/servicos")]
    public class ServicosController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_AGENDADO = 0;
        private const int ESTADO_EM_CURSO = 1;
        private const int ESTADO_CONCLUIDO = 2;

        // Convenção sugerida para a app móvel / operação
        private const int TIPO_MANUTENCAO = 1;
        private const int TIPO_AVARIA = 2;
        private const int TIPO_GARANTIA = 3;
        private const int TIPO_INSPECAO = 4;
        private const int TIPO_DIAGNOSTICO = 5;
        private const int TIPO_PREPARACAO_ENTREGA = 6;
        private const int TIPO_CAMPANHA_TECNICA = 7;
        private const int TIPO_OUTRO = 8;

        public ServicosController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/servicos/meta
        [HttpGet("meta")]
        public IActionResult GetMeta()
        {
            return Ok(new
            {
                estados = new[]
                {
                    new { id = ESTADO_AGENDADO, nome = "Agendado" },
                    new { id = ESTADO_EM_CURSO, nome = "Em Curso" },
                    new { id = ESTADO_CONCLUIDO, nome = "Concluído" }
                },
                tipos = new[]
                {
                    new { id = TIPO_MANUTENCAO, nome = "Manutenção" },
                    new { id = TIPO_AVARIA, nome = "Avaria" },
                    new { id = TIPO_GARANTIA, nome = "Garantia" },
                    new { id = TIPO_INSPECAO, nome = "Inspeção" },
                    new { id = TIPO_DIAGNOSTICO, nome = "Diagnóstico" },
                    new { id = TIPO_PREPARACAO_ENTREGA, nome = "Preparação / Entrega" },
                    new { id = TIPO_CAMPANHA_TECNICA, nome = "Campanha Técnica" },
                    new { id = TIPO_OUTRO, nome = "Outro" }
                }
            });
        }

        // GET /api/servicos?estado=1&motaId=10&modeloId=3&tipo=2&vin=ABC&emAberto=true&q=travão
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? estado = null,
            [FromQuery] int? motaId = null,
            [FromQuery] int? modeloId = null,
            [FromQuery] int? tipo = null,
            [FromQuery] string? vin = null,
            [FromQuery] bool? emAberto = null,
            [FromQuery] string? q = null)
        {
            if (estado.HasValue && !EstadoValido(estado.Value))
                return BadRequest(new { message = "Estado inválido. Use 0=Agendado, 1=Em Curso, 2=Concluído." });

            if (tipo.HasValue && !TipoValido(tipo.Value))
                return BadRequest(new { message = "Tipo inválido. Consulta GET /api/servicos/meta." });

            if (motaId.HasValue && motaId.Value <= 0)
                return BadRequest(new { message = "motaId inválido." });

            if (modeloId.HasValue && modeloId.Value <= 0)
                return BadRequest(new { message = "modeloId inválido." });

            var query =
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    s.Tipo,
                    s.Descricao,
                    s.Estado,
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico,
                    m.IDModelo,
                    m.IDOrdemProducao,
                    m.NumeroIdentificacao,
                    m.Cor,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                };

            if (estado.HasValue)
                query = query.Where(x => x.Estado == estado.Value);

            if (motaId.HasValue)
                query = query.Where(x => x.IDMota == motaId.Value);

            if (modeloId.HasValue)
                query = query.Where(x => x.IDModelo == modeloId.Value);

            if (tipo.HasValue)
                query = query.Where(x => x.Tipo == tipo.Value);

            if (emAberto.HasValue && emAberto.Value)
                query = query.Where(x => x.Estado != ESTADO_CONCLUIDO);

            if (!string.IsNullOrWhiteSpace(vin))
            {
                var vinNormalizado = vin.Trim().ToUpper();
                query = query.Where(x =>
                    !string.IsNullOrWhiteSpace(x.NumeroIdentificacao) &&
                    x.NumeroIdentificacao!.ToUpper() == vinNormalizado);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var termo = $"%{q.Trim()}%";
                query = query.Where(x =>
                    EF.Functions.Like(x.Descricao ?? "", termo) ||
                    EF.Functions.Like(x.NotasServico ?? "", termo) ||
                    EF.Functions.Like(x.NumeroIdentificacao ?? "", termo) ||
                    EF.Functions.Like(x.modeloNome ?? "", termo) ||
                    EF.Functions.Like(x.modeloCodigo ?? "", termo));
            }

            var lista = await query
                .OrderByDescending(x => x.DataServico)
                .ThenByDescending(x => x.IDServico)
                .ToListAsync();

            return Ok(lista);
        }

        // GET /api/servicos/em-aberto
        [HttpGet("em-aberto")]
        public async Task<IActionResult> GetEmAberto()
        {
            var lista = await (
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where s.Estado != ESTADO_CONCLUIDO
                orderby s.DataServico descending, s.IDServico descending
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    s.Tipo,
                    s.Descricao,
                    s.Estado,
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico,
                    m.IDModelo,
                    m.IDOrdemProducao,
                    m.NumeroIdentificacao,
                    m.Cor,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                }
            ).ToListAsync();

            return Ok(new
            {
                total = lista.Count,
                servicos = lista
            });
        }

        // GET /api/servicos/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var servico = await (
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where s.IDServico == id
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    s.Tipo,
                    s.Descricao,
                    s.Estado,
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico,
                    mota = new
                    {
                        m.IDMota,
                        m.IDModelo,
                        m.IDOrdemProducao,
                        m.NumeroIdentificacao,
                        m.Cor,
                        m.Quilometragem,
                        m.Estado,
                        modelo = new
                        {
                            mm.IDModelo,
                            mm.Nome,
                            mm.CodigoProduto
                        }
                    }
                }
            ).FirstOrDefaultAsync();

            if (servico == null)
                return NotFound(new { message = "Serviço não encontrado." });

            var pecasAlteradas = await BuildPecasAlteradasListAsync(id);

            return Ok(new
            {
                servico.IDServico,
                servico.IDMota,
                servico.Tipo,
                tipoNome = GetTipoNome(servico.Tipo),
                servico.Descricao,
                servico.Estado,
                estadoNome = GetEstadoNome(servico.Estado),
                servico.DataServico,
                servico.DataConclusao,
                servico.NotasServico,
                servico.mota,
                pecasAlteradas
            });
        }

        // POST /api/servicos
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateServicoRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.IDMota <= 0)
                return BadRequest(new { message = "IDMota é obrigatório." });

            if (!EstadoValido(req.Estado))
                return BadRequest(new { message = "Estado inválido. Use 0=Agendado, 1=Em Curso, 2=Concluído." });

            if (!TipoValido(req.Tipo))
                return BadRequest(new { message = "Tipo inválido. Consulta GET /api/servicos/meta." });

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDMota == req.IDMota);

            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var estado = req.Estado;
            var dataServico = req.DataServico ?? DateTime.UtcNow;
            DateTime? dataConclusao = req.DataConclusao;

            if (estado == ESTADO_CONCLUIDO && dataConclusao == null)
                dataConclusao = DateTime.UtcNow;

            if (estado != ESTADO_CONCLUIDO && dataConclusao.HasValue)
            {
                return BadRequest(new
                {
                    message = "DataConclusao só deve ser preenchida quando o serviço está concluído."
                });
            }

            if (dataConclusao.HasValue && dataConclusao.Value < dataServico)
            {
                return BadRequest(new
                {
                    message = "DataConclusao não pode ser anterior à DataServico."
                });
            }

            var servico = new Servico
            {
                IDMota = req.IDMota,
                Tipo = req.Tipo,
                Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim(),
                Estado = estado,
                DataServico = dataServico,
                DataConclusao = dataConclusao,
                NotasServico = string.IsNullOrWhiteSpace(req.NotasServico) ? null : req.NotasServico.Trim()
            };

            _db.Set<Servico>().Add(servico);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                servico.IDServico,
                servico.IDMota,
                servico.Tipo,
                tipoNome = GetTipoNome(servico.Tipo),
                servico.Estado,
                estadoNome = GetEstadoNome(servico.Estado),
                servico.DataServico,
                servico.DataConclusao
            });
        }

        // PUT /api/servicos/{id}/estado
        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> UpdateEstado(int id, [FromBody] UpdateServicoEstadoRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (!EstadoValido(req.Estado))
                return BadRequest(new { message = "Estado inválido. Use 0=Agendado, 1=Em Curso, 2=Concluído." });

            var servico = await _db.Set<Servico>().FirstOrDefaultAsync(x => x.IDServico == id);
            if (servico == null)
                return NotFound(new { message = "Serviço não encontrado." });

            if (req.Estado != ESTADO_CONCLUIDO && req.DataConclusao.HasValue)
            {
                return BadRequest(new
                {
                    message = "DataConclusao só pode ser preenchida quando o serviço está concluído."
                });
            }

            servico.Estado = req.Estado;

            if (req.Estado == ESTADO_CONCLUIDO)
            {
                var dataConclusao = req.DataConclusao ?? DateTime.UtcNow;

                if (dataConclusao < servico.DataServico)
                {
                    return BadRequest(new
                    {
                        message = "DataConclusao não pode ser anterior à DataServico."
                    });
                }

                servico.DataConclusao = dataConclusao;
            }
            else
            {
                servico.DataConclusao = null;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Estado do serviço atualizado com sucesso.",
                servico.IDServico,
                servico.Estado,
                estadoNome = GetEstadoNome(servico.Estado),
                servico.DataConclusao
            });
        }

        // GET /api/servicos/{id}/pecas-alteradas
        [HttpGet("{id:int}/pecas-alteradas")]
        public async Task<IActionResult> GetPecasAlteradas(int id)
        {
            var servicoExists = await _db.Set<Servico>()
                .AsNoTracking()
                .AnyAsync(s => s.IDServico == id);

            if (!servicoExists)
                return NotFound(new { message = "Serviço não encontrado." });

            var lista = await BuildPecasAlteradasListAsync(id);
            return Ok(lista);
        }

        // POST /api/servicos/{id}/pecas-alteradas
        [HttpPost("{id:int}/pecas-alteradas")]
        public async Task<IActionResult> AddPecaAlterada(int id, [FromBody] AddPecaAlteradaRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.IDMotasPecasSN <= 0)
                return BadRequest(new { message = "IDMotasPecasSN é obrigatório." });

            var servico = await _db.Set<Servico>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IDServico == id);

            if (servico == null)
                return NotFound(new { message = "Serviço não encontrado." });

            var motaPeca = await (
                from mps in _db.Set<MotasPecasSN>().AsNoTracking()
                join p in _db.Set<Peca>().AsNoTracking() on mps.IDPeca equals p.IDPeca
                where mps.IDMotasPecasSN == req.IDMotasPecasSN
                select new
                {
                    mps.IDMotasPecasSN,
                    mps.IDMota,
                    mps.IDPeca,
                    mps.NumeroSerie,
                    p.PartNumber,
                    p.Descricao
                }
            ).FirstOrDefaultAsync();

            if (motaPeca == null)
                return NotFound(new { message = "A peça serializada indicada não foi encontrada." });

            if (motaPeca.IDMota != servico.IDMota)
            {
                return BadRequest(new
                {
                    message = "A peça serializada indicada não pertence à mesma mota deste serviço."
                });
            }

            var jaExiste = await _db.Set<ServicosPecasAlterada>()
                .AsNoTracking()
                .AnyAsync(x => x.IDServico == id && x.IDMotasPecasSN == req.IDMotasPecasSN);

            if (jaExiste)
            {
                return Conflict(new
                {
                    message = "Essa peça já está registada como alterada neste serviço."
                });
            }

            var novoNumeroSerie = string.IsNullOrWhiteSpace(req.NovoNumeroSerie)
                ? null
                : req.NovoNumeroSerie.Trim();

            if (!string.IsNullOrWhiteSpace(novoNumeroSerie))
            {
                var snDuplicado = await _db.Set<MotasPecasSN>()
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.IDPeca == motaPeca.IDPeca &&
                        x.IDMotasPecasSN != req.IDMotasPecasSN &&
                        x.NumeroSerie == novoNumeroSerie);

                if (snDuplicado)
                {
                    return Conflict(new
                    {
                        message = "Já existe outra mota com esse número de série para esta peça."
                    });
                }
            }

            using var tx = await _db.Database.BeginTransactionAsync();

            var observacoesBase = string.IsNullOrWhiteSpace(req.Observacoes)
                ? null
                : req.Observacoes.Trim();

            string? observacoesFinal = observacoesBase;

            if (!string.IsNullOrWhiteSpace(novoNumeroSerie))
            {
                var snAnteriorTexto = string.IsNullOrWhiteSpace(motaPeca.NumeroSerie) ? "(vazio)" : motaPeca.NumeroSerie;
                var complemento = $"SN anterior: {snAnteriorTexto} | SN novo: {novoNumeroSerie}";

                observacoesFinal = string.IsNullOrWhiteSpace(observacoesBase)
                    ? complemento
                    : $"{observacoesBase} | {complemento}";
            }

            var assoc = new ServicosPecasAlterada
            {
                IDServico = id,
                IDMotasPecasSN = req.IDMotasPecasSN,
                Observacoes = observacoesFinal
            };

            _db.Set<ServicosPecasAlterada>().Add(assoc);

            if (!string.IsNullOrWhiteSpace(novoNumeroSerie))
            {
                var motaPecaEditable = await _db.Set<MotasPecasSN>()
                    .FirstOrDefaultAsync(x => x.IDMotasPecasSN == req.IDMotasPecasSN);

                if (motaPecaEditable != null)
                    motaPecaEditable.NumeroSerie = novoNumeroSerie;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var lista = await BuildPecasAlteradasListAsync(id);

            return Ok(new
            {
                message = "Peça alterada registada com sucesso.",
                assoc.ID,
                pecasAlteradas = lista
            });
        }

        // DELETE /api/servicos/pecas-alteradas/{idAssoc}
        [HttpDelete("pecas-alteradas/{idAssoc:int}")]
        public async Task<IActionResult> DeleteAssoc(int idAssoc)
        {
            var assoc = await _db.Set<ServicosPecasAlterada>()
                .FirstOrDefaultAsync(x => x.ID == idAssoc);

            if (assoc == null)
                return NotFound(new { message = "Associação de peça alterada não encontrada." });

            _db.Set<ServicosPecasAlterada>().Remove(assoc);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // GET /api/servicos/motas/{motaId}/historico
        [HttpGet("motas/{motaId:int}/historico")]
        public async Task<IActionResult> GetHistoricoByMota(int motaId)
        {
            var mota = await (
                from m in _db.Set<Mota>().AsNoTracking()
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where m.IDMota == motaId
                select new
                {
                    m.IDMota,
                    m.IDModelo,
                    m.NumeroIdentificacao,
                    m.Cor,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                }
            ).FirstOrDefaultAsync();

            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var servicos = await _db.Set<Servico>()
                .AsNoTracking()
                .Where(s => s.IDMota == motaId)
                .OrderByDescending(s => s.DataServico)
                .Select(s => new
                {
                    s.IDServico,
                    s.Tipo,
                    tipoNome = GetTipoNome(s.Tipo),
                    s.Descricao,
                    s.Estado,
                    estadoNome = GetEstadoNome(s.Estado),
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico
                })
                .ToListAsync();

            return Ok(new
            {
                mota,
                totalServicos = servicos.Count,
                concluidos = servicos.Count(x => x.Estado == ESTADO_CONCLUIDO),
                emCurso = servicos.Count(x => x.Estado == ESTADO_EM_CURSO),
                agendados = servicos.Count(x => x.Estado == ESTADO_AGENDADO),
                servicos
            });
        }

        // GET /api/servicos/by-vin/{vin}/historico
        [HttpGet("by-vin/{vin}/historico")]
        public async Task<IActionResult> GetHistoricoByVin(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin))
                return BadRequest(new { message = "VIN / Número de Identificação é obrigatório." });

            var vinNormalizado = vin.Trim().ToUpper();

            var mota = await (
                from m in _db.Set<Mota>().AsNoTracking()
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where !string.IsNullOrWhiteSpace(m.NumeroIdentificacao)
                      && m.NumeroIdentificacao!.ToUpper() == vinNormalizado
                select new
                {
                    m.IDMota,
                    m.IDModelo,
                    m.NumeroIdentificacao,
                    m.Cor,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                }
            ).FirstOrDefaultAsync();

            if (mota == null)
                return NotFound(new { message = "Mota não encontrada com esse VIN / Número de Identificação." });

            var servicos = await _db.Set<Servico>()
                .AsNoTracking()
                .Where(s => s.IDMota == mota.IDMota)
                .OrderByDescending(s => s.DataServico)
                .Select(s => new
                {
                    s.IDServico,
                    s.Tipo,
                    tipoNome = GetTipoNome(s.Tipo),
                    s.Descricao,
                    s.Estado,
                    estadoNome = GetEstadoNome(s.Estado),
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico
                })
                .ToListAsync();

            return Ok(new
            {
                mota,
                totalServicos = servicos.Count,
                concluidos = servicos.Count(x => x.Estado == ESTADO_CONCLUIDO),
                emCurso = servicos.Count(x => x.Estado == ESTADO_EM_CURSO),
                agendados = servicos.Count(x => x.Estado == ESTADO_AGENDADO),
                servicos
            });
        }

        // GET /api/servicos/modelos/{idModelo}/historico
        [HttpGet("modelos/{idModelo:int}/historico")]
        public async Task<IActionResult> GetHistoricoByModelo(int idModelo)
        {
            var modelo = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .Where(m => m.IDModelo == idModelo)
                .Select(m => new
                {
                    m.IDModelo,
                    m.Nome,
                    m.CodigoProduto
                })
                .FirstOrDefaultAsync();

            if (modelo == null)
                return NotFound(new { message = "Modelo não encontrado." });

            var servicos = await (
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                where m.IDModelo == idModelo
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    m.NumeroIdentificacao,
                    s.Tipo,
                    tipoNome = GetTipoNome(s.Tipo),
                    s.Descricao,
                    s.Estado,
                    estadoNome = GetEstadoNome(s.Estado),
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico
                }
            ).OrderByDescending(x => x.DataServico).ToListAsync();

            var problemasMaisRegistados = servicos
                .Where(x => !string.IsNullOrWhiteSpace(x.Descricao))
                .GroupBy(x => x.Descricao!.Trim().ToLower())
                .Select(g => new
                {
                    descricao = g.First().Descricao,
                    total = g.Count()
                })
                .OrderByDescending(x => x.total)
                .ThenBy(x => x.descricao)
                .Take(10)
                .ToList();

            var porTipo = servicos
                .GroupBy(x => new { x.Tipo, x.tipoNome })
                .Select(g => new
                {
                    tipo = g.Key.Tipo,
                    nome = g.Key.tipoNome,
                    total = g.Count()
                })
                .OrderByDescending(x => x.total)
                .ToList();

            var ultimosServicos = servicos
                .Take(20)
                .ToList();

            return Ok(new
            {
                modelo,
                totalServicos = servicos.Count,
                totalMotasComHistorico = servicos.Select(x => x.IDMota).Distinct().Count(),
                concluidos = servicos.Count(x => x.Estado == ESTADO_CONCLUIDO),
                emCurso = servicos.Count(x => x.Estado == ESTADO_EM_CURSO),
                agendados = servicos.Count(x => x.Estado == ESTADO_AGENDADO),
                porTipo,
                problemasMaisRegistados,
                ultimosServicos
            });
        }

        // GET /api/servicos/modelos/{idModelo}/problemas-frequentes
        [HttpGet("modelos/{idModelo:int}/problemas-frequentes")]
        public async Task<IActionResult> GetProblemasFrequentesByModelo(int idModelo)
        {
            var modelo = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .Where(m => m.IDModelo == idModelo)
                .Select(m => new
                {
                    m.IDModelo,
                    m.Nome,
                    m.CodigoProduto
                })
                .FirstOrDefaultAsync();

            if (modelo == null)
                return NotFound(new { message = "Modelo não encontrado." });

            var servicos = await (
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                where m.IDModelo == idModelo && !string.IsNullOrWhiteSpace(s.Descricao)
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    m.NumeroIdentificacao,
                    s.Tipo,
                    s.Descricao,
                    s.Estado,
                    s.DataServico
                }
            ).ToListAsync();

            var problemas = servicos
                .GroupBy(x => x.Descricao!.Trim().ToLower())
                .Select(g => new
                {
                    descricao = g.First().Descricao,
                    total = g.Count(),
                    totalMotas = g.Select(x => x.IDMota).Distinct().Count(),
                    ultimosCasos = g
                        .OrderByDescending(x => x.DataServico)
                        .Take(5)
                        .Select(x => new
                        {
                            x.IDServico,
                            x.IDMota,
                            x.NumeroIdentificacao,
                            x.Tipo,
                            tipoNome = GetTipoNome(x.Tipo),
                            x.Estado,
                            estadoNome = GetEstadoNome(x.Estado),
                            x.DataServico
                        })
                        .ToList()
                })
                .OrderByDescending(x => x.total)
                .ThenBy(x => x.descricao)
                .Take(15)
                .ToList();

            return Ok(new
            {
                modelo,
                totalProblemasAgrupados = problemas.Count,
                problemas
            });
        }

        // GET /api/servicos/modelos/{idModelo}/garantias
        [HttpGet("modelos/{idModelo:int}/garantias")]
        public async Task<IActionResult> GetGarantiasByModelo(int idModelo)
        {
            var modelo = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .Where(m => m.IDModelo == idModelo)
                .Select(m => new
                {
                    m.IDModelo,
                    m.Nome,
                    m.CodigoProduto
                })
                .FirstOrDefaultAsync();

            if (modelo == null)
                return NotFound(new { message = "Modelo não encontrado." });

            var garantias = await (
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                where m.IDModelo == idModelo && s.Tipo == TIPO_GARANTIA
                orderby s.DataServico descending
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    m.NumeroIdentificacao,
                    s.Tipo,
                    tipoNome = GetTipoNome(s.Tipo),
                    s.Descricao,
                    s.Estado,
                    estadoNome = GetEstadoNome(s.Estado),
                    s.DataServico,
                    s.DataConclusao,
                    s.NotasServico
                }
            ).ToListAsync();

            return Ok(new
            {
                modelo,
                total = garantias.Count,
                servicos = garantias
            });
        }

        private async Task<List<object>> BuildPecasAlteradasListAsync(int servicoId)
        {
            var lista = await (
                from spa in _db.Set<ServicosPecasAlterada>().AsNoTracking()
                join mps in _db.Set<MotasPecasSN>().AsNoTracking() on spa.IDMotasPecasSN equals mps.IDMotasPecasSN
                join p in _db.Set<Peca>().AsNoTracking() on mps.IDPeca equals p.IDPeca
                where spa.IDServico == servicoId
                orderby p.PartNumber
                select new
                {
                    spa.ID,
                    spa.IDServico,
                    spa.IDMotasPecasSN,
                    spa.Observacoes,
                    mps.IDMota,
                    mps.IDPeca,
                    p.PartNumber,
                    p.Descricao,
                    mps.NumeroSerie,
                    numeroSerieAtual = mps.NumeroSerie
                }
            ).ToListAsync();

            return lista.Cast<object>().ToList();
        }

        private static bool EstadoValido(int estado)
        {
            return estado == ESTADO_AGENDADO ||
                   estado == ESTADO_EM_CURSO ||
                   estado == ESTADO_CONCLUIDO;
        }

        private static bool TipoValido(int tipo)
        {
            return tipo == TIPO_MANUTENCAO ||
                   tipo == TIPO_AVARIA ||
                   tipo == TIPO_GARANTIA ||
                   tipo == TIPO_INSPECAO ||
                   tipo == TIPO_DIAGNOSTICO ||
                   tipo == TIPO_PREPARACAO_ENTREGA ||
                   tipo == TIPO_CAMPANHA_TECNICA ||
                   tipo == TIPO_OUTRO;
        }

        private static string GetEstadoNome(int estado)
        {
            return estado switch
            {
                ESTADO_AGENDADO => "Agendado",
                ESTADO_EM_CURSO => "Em Curso",
                ESTADO_CONCLUIDO => "Concluído",
                _ => "Desconhecido"
            };
        }

        private static string GetTipoNome(int tipo)
        {
            return tipo switch
            {
                TIPO_MANUTENCAO => "Manutenção",
                TIPO_AVARIA => "Avaria",
                TIPO_GARANTIA => "Garantia",
                TIPO_INSPECAO => "Inspeção",
                TIPO_DIAGNOSTICO => "Diagnóstico",
                TIPO_PREPARACAO_ENTREGA => "Preparação / Entrega",
                TIPO_CAMPANHA_TECNICA => "Campanha Técnica",
                TIPO_OUTRO => "Outro",
                _ => "Desconhecido"
            };
        }
    }
}