using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class ValidacaoFinalResultado
    {
        public int OrdemId { get; set; }
        public bool PodeFinalizar { get; set; }
        public bool VinOk { get; set; }
        public bool PecasSnOk { get; set; }
        public bool MontagemOk { get; set; }
        public bool EmbalagemOk { get; set; }
        public bool ControloFinalOk { get; set; }
        public List<string> Pendencias { get; set; } = new();
    }

    [ApiController]
    [Route("api/controlo-fabrica")]
    public class ControloFabricaController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_ABERTA = 0;
        private const int ESTADO_EM_PRODUCAO = 1;
        private const int ESTADO_CONCLUIDA = 2;
        private const int ESTADO_BLOQUEADA = 3;

        private const int ESTADO_MOTA_EM_PRODUCAO = 0;
        private const int ESTADO_MOTA_ATIVA = 1;

        private const int ESTADO_UTILIZADOR_ATIVO = 1;
        private const int ESTADO_SERVICO_CONCLUIDO = 2;
        private const int TIPO_SERVICO_AVARIA = 2;
        private const int TIPO_SERVICO_GARANTIA = 3;
        private const int LIMIAR_DIAS_SERVICO_ABERTO = 7;

        public ControloFabricaController(LcmContext db) => _db = db;

        // GET /api/controlo-fabrica/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var ordens = await _db.Set<OrdemProducao>().AsNoTracking()
                .OrderByDescending(o => o.DataCriacao)
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.Estado,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .ToListAsync();

            var motas = await _db.Set<Mota>().AsNoTracking()
                .Select(m => new { m.IDMota, m.IDOrdemProducao, m.NumeroIdentificacao })
                .ToListAsync();

            var pendentesControloSet = (await _db.Set<ChecklistControlo>().AsNoTracking()
                .Where(c => c.ControloFinal == 0).Select(c => c.IDOrdemProducao).Distinct().ToListAsync()).ToHashSet();

            var equipaAtiva = await _db.Set<UtilizadorMotum>().AsNoTracking().CountAsync(u => u.Estado == ESTADO_UTILIZADOR_ATIVO);
            var servicosEmAberto = await _db.Set<Servico>().AsNoTracking().CountAsync(s => s.Estado != ESTADO_SERVICO_CONCLUIDO);

            var clienteIds = ordens.Where(o => o.idCliente.HasValue).Select(o => o.idCliente!.Value).Distinct().ToList();
            var modeloIds = ordens.Where(o => o.idModelo.HasValue).Select(o => o.idModelo!.Value).Distinct().ToList();

            var clientesDict = new Dictionary<int, string>();
            if (clienteIds.Count > 0)
            {
                var cl = await _db.Set<Cliente>().AsNoTracking().Where(c => clienteIds.Contains(c.IDCliente)).Select(c => new { c.IDCliente, c.Nome }).ToListAsync();
                foreach (var c in cl) clientesDict[c.IDCliente] = c.Nome;
            }

            var modelosDict = new Dictionary<int, string>();
            if (modeloIds.Count > 0)
            {
                var ml = await _db.Set<ModelosMotum>().AsNoTracking().Where(m => modeloIds.Contains(m.IDModelo)).Select(m => new { m.IDModelo, m.Nome }).ToListAsync();
                foreach (var m in ml) modelosDict[m.IDModelo] = m.Nome;
            }

            var motasPorOrdem = motas.Where(m => m.IDOrdemProducao.HasValue).ToDictionary(m => m.IDOrdemProducao!.Value);
            var ordemIdsComMota = motasPorOrdem.Keys.ToHashSet();

            var ordensResumo = ordens.Select(o =>
            {
                var mota = motasPorOrdem.GetValueOrDefault(o.IDOrdemProducao);
                return new
                {
                    ordemId = o.IDOrdemProducao,
                    numeroOrdem = o.NumeroOrdem,
                    estado = o.Estado,
                    estadoNome = GetEstadoOrdemNome(o.Estado),
                    modeloNome = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value) : null,
                    clienteNome = o.idCliente.HasValue ? clientesDict.GetValueOrDefault(o.idCliente.Value) : null,
                    temMota = mota != null,
                    vinPreenchido = mota != null && !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao),
                    controloPendente = pendentesControloSet.Contains(o.IDOrdemProducao)
                };
            }).ToList();

            return Ok(new
            {
                totalOrdens = ordens.Count,
                emProducao = ordens.Count(o => o.Estado == ESTADO_EM_PRODUCAO),
                bloqueadas = ordens.Count(o => o.Estado == ESTADO_BLOQUEADA),
                semUnidade = ordens.Count(o => o.Estado == ESTADO_EM_PRODUCAO && !ordemIdsComMota.Contains(o.IDOrdemProducao)),
                controloPendente = pendentesControloSet.Count,
                vinPendente = motas.Count(m => string.IsNullOrWhiteSpace(m.NumeroIdentificacao)),
                equipaAtiva,
                servicosEmAberto,
                ordens = ordensResumo
            });
        }

        // GET /api/controlo-fabrica/ordens?estado=1
        [HttpGet("ordens")]
        public async Task<IActionResult> GetOrdens([FromQuery] int? estado = null)
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
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .ToListAsync();

            var clienteIds = ordens.Where(o => o.idCliente.HasValue).Select(o => o.idCliente!.Value).Distinct().ToList();
            var modeloIds = ordens.Where(o => o.idModelo.HasValue).Select(o => o.idModelo!.Value).Distinct().ToList();

            var clientesDict = new Dictionary<int, string>();
            if (clienteIds.Count > 0)
            {
                var cl = await _db.Set<Cliente>().AsNoTracking().Where(c => clienteIds.Contains(c.IDCliente)).Select(c => new { c.IDCliente, c.Nome }).ToListAsync();
                foreach (var c in cl) clientesDict[c.IDCliente] = c.Nome;
            }

            var modelosDict = new Dictionary<int, (string Nome, string Codigo)>();
            if (modeloIds.Count > 0)
            {
                var ml = await _db.Set<ModelosMotum>().AsNoTracking().Where(m => modeloIds.Contains(m.IDModelo)).Select(m => new { m.IDModelo, m.Nome, m.CodigoProduto }).ToListAsync();
                foreach (var m in ml) modelosDict[m.IDModelo] = (m.Nome, m.CodigoProduto);
            }

            return Ok(ordens.Select(o => new
            {
                o.IDOrdemProducao,
                o.NumeroOrdem,
                o.Estado,
                estadoNome = GetEstadoOrdemNome(o.Estado),
                o.PaisDestino,
                o.DataCriacao,
                o.DataConclusao,
                idModelo = o.idModelo,
                idCliente = o.idCliente,
                clienteNome = o.idCliente.HasValue ? clientesDict.GetValueOrDefault(o.idCliente.Value) : null,
                modeloNome = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).Nome : null,
                modeloCodigo = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).Codigo : null
            }));
        }

        // GET /api/controlo-fabrica/ordens/{id}
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
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .FirstOrDefaultAsync();

            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            var mota = await _db.Set<Mota>().AsNoTracking()
                .Where(m => m.IDOrdemProducao == id)
                .Select(m => new { idMota = m.IDMota, m.NumeroIdentificacao, vinPreenchido = !string.IsNullOrWhiteSpace(m.NumeroIdentificacao), m.Cor, m.Estado, m.DataRegisto })
                .FirstOrDefaultAsync();

            return Ok(new { ordem, mota });
        }

        // GET /api/controlo-fabrica/ordens/{id}/ficha
        [HttpGet("ordens/{id:int}/ficha")]
        public async Task<IActionResult> GetFicha(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            ModeloFichaDto? modelo = null;
            if (ordem.ModeloMotaIDModelo.HasValue)
                modelo = await _db.Set<ModelosMotum>().AsNoTracking()
                    .Where(m => m.IDModelo == ordem.ModeloMotaIDModelo.Value)
                    .Select(m => new ModeloFichaDto { IDModelo = m.IDModelo, Nome = m.Nome, CodigoProduto = m.CodigoProduto, Estado = m.Estado })
                    .FirstOrDefaultAsync();

            ClienteFichaDto? cliente = null;
            if (ordem.ClienteIDCliente.HasValue)
                cliente = await _db.Set<Cliente>().AsNoTracking()
                    .Where(c => c.IDCliente == ordem.ClienteIDCliente.Value)
                    .Select(c => new ClienteFichaDto { IDCliente = c.IDCliente, Nome = c.Nome, Tipo = c.Tipo })
                    .FirstOrDefaultAsync();

            var encomenda = await _db.Set<Encomenda>().AsNoTracking()
                .Where(e => e.IDEncomenda == ordem.IDEncomenda)
                .Select(e => new EncomendaFichaDto { IDEncomenda = e.IDEncomenda, Quantidade = e.Quantidade, Estado = e.Estado, DateCriacao = e.DateCriacao, DataEntrega = e.DataEntrega })
                .FirstOrDefaultAsync();

            var motaRaw = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
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

            var utilizadoresAtivos = motaRaw == null ? 0 :
                await _db.Set<UtilizadorMotum>().AsNoTracking().CountAsync(x => x.IDMota == motaRaw.IDMota && x.Estado == 1);

            var servicosCount = motaRaw == null ? 0 :
                await _db.Set<Servico>().AsNoTracking().CountAsync(s => s.IDMota == motaRaw.IDMota);

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
                utilizadoresAtivos,
                servicosCount
            });
        }

        // GET /api/controlo-fabrica/ordens/{id}/historico
        [HttpGet("ordens/{id:int}/historico")]
        public async Task<IActionResult> GetHistorico(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            var temChecklists = await _db.Set<ChecklistMontagem>().AsNoTracking().AnyAsync(c => c.IDOrdemProducao == id);

            var eventos = new List<HistoricoEventoDto>();
            int seq = 1;

            eventos.Add(new HistoricoEventoDto { Id = seq++, Tipo = "CRIACAO", Descricao = $"Ordem {ordem.NumeroOrdem} criada.", DataOcorrencia = ordem.DataCriacao.ToString("o"), ValorNovo = "Aberta" });

            if (ordem.Estado >= ESTADO_EM_PRODUCAO || temChecklists)
                eventos.Add(new HistoricoEventoDto { Id = seq++, Tipo = "ESTADO", Descricao = "Ordem iniciada em produção.", DataOcorrencia = ordem.DataCriacao.ToString("o"), ValorAnterior = "Aberta", ValorNovo = "Em Produção" });

            if (mota != null)
            {
                eventos.Add(new HistoricoEventoDto { Id = seq++, Tipo = "UNIDADE", Descricao = $"Unidade #{mota.IDMota} registada.", DataOcorrencia = mota.DataRegisto.ToString("o"), ValorNovo = $"#{mota.IDMota}" });
                if (!string.IsNullOrWhiteSpace(mota.NumeroIdentificacao))
                    eventos.Add(new HistoricoEventoDto { Id = seq++, Tipo = "VIN", Descricao = $"VIN registado: {mota.NumeroIdentificacao}.", DataOcorrencia = mota.DataRegisto.ToString("o"), ValorNovo = mota.NumeroIdentificacao });
            }

            if (ordem.DataConclusao.HasValue)
                eventos.Add(new HistoricoEventoDto { Id = seq++, Tipo = "ESTADO", Descricao = "Ordem finalizada.", DataOcorrencia = ordem.DataConclusao.Value.ToString("o"), ValorAnterior = "Em Produção", ValorNovo = "Concluída" });

            if (ordem.Estado == ESTADO_BLOQUEADA)
                eventos.Add(new HistoricoEventoDto { Id = seq++, Tipo = "ESTADO", Descricao = "Ordem bloqueada.", DataOcorrencia = (mota?.DataRegisto ?? ordem.DataCriacao).ToString("o"), ValorAnterior = "Em Produção", ValorNovo = "Bloqueada" });

            return Ok(new
            {
                ordemId = id,
                numeroOrdem = ordem.NumeroOrdem,
                aviso = "Histórico calculado. Sem tabela de auditoria — timestamps aproximados.",
                total = eventos.Count,
                historico = eventos
            });
        }

        // POST /api/controlo-fabrica/ordens/{id}/bloquear
        [HttpPost("ordens/{id:int}/bloquear")]
        public async Task<IActionResult> BloquearOrdem(int id, [FromBody] BloquearOrdemRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Motivo))
                return BadRequest(new { message = "O motivo do bloqueio é obrigatório." });

            var ordem = await _db.Set<OrdemProducao>().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
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
                message = "Ordem bloqueada.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                motivo = req.Motivo.Trim(),
                aviso = "O motivo foi registado mas não persiste na BD atual."
            });
        }

        // POST /api/controlo-fabrica/ordens/{id}/desbloquear
        [HttpPost("ordens/{id:int}/desbloquear")]
        public async Task<IActionResult> DesbloquearOrdem(int id, [FromBody] DesbloquearOrdemRequest? req = null)
        {
            var ordem = await _db.Set<OrdemProducao>().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_BLOQUEADA)
                return Conflict(new { message = "A ordem não está bloqueada." });

            var temChecklists = await _db.Set<ChecklistMontagem>().AsNoTracking().AnyAsync(x => x.IDOrdemProducao == id);
            ordem.Estado = temChecklists ? ESTADO_EM_PRODUCAO : ESTADO_ABERTA;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = $"Ordem desbloqueada. Estado: '{GetEstadoOrdemNome(ordem.Estado)}'.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado)
            });
        }

        // POST /api/controlo-fabrica/ordens/{id}/reabrir
        [HttpPost("ordens/{id:int}/reabrir")]
        public async Task<IActionResult> ReabrirOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_CONCLUIDA)
                return Conflict(new { message = "Apenas ordens concluídas podem ser reabertas." });

            var mota = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            ordem.Estado = ESTADO_EM_PRODUCAO;
            ordem.DataConclusao = null;

            if (mota != null && mota.Estado == ESTADO_MOTA_ATIVA)
                mota.Estado = ESTADO_MOTA_EM_PRODUCAO;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Ordem reaberta.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado)
            });
        }

        // GET /api/controlo-fabrica/alertas?ordemId=X&tipo=BLOQUEIO&severidade=CRITICA
        [HttpGet("alertas")]
        public async Task<IActionResult> GetAlertas(
            [FromQuery] int? ordemId = null,
            [FromQuery] string? tipo = null,
            [FromQuery] string? severidade = null)
        {
            var alertas = new List<AlertaDto>();
            var agora = DateTime.UtcNow;

            // Ordens bloqueadas
            var qBloqueadas = _db.Set<OrdemProducao>().AsNoTracking().Where(o => o.Estado == ESTADO_BLOQUEADA);
            if (ordemId.HasValue) qBloqueadas = qBloqueadas.Where(o => o.IDOrdemProducao == ordemId.Value);
            var bloqueadas = await qBloqueadas.Select(o => new { o.IDOrdemProducao, o.NumeroOrdem, o.DataCriacao, idModelo = o.ModeloMotaIDModelo, idCliente = o.ClienteIDCliente }).ToListAsync();
            foreach (var o in bloqueadas)
                alertas.Add(new AlertaDto { Tipo = "BLOQUEIO", Severidade = "CRITICA", Descricao = $"Ordem {o.NumeroOrdem} está bloqueada e não avança em produção.", OrdemId = o.IDOrdemProducao, ModeloId = o.idModelo, ClienteId = o.idCliente, DataCriacaoIso = o.DataCriacao.ToString("o") });

            // Ordens EM_PRODUCAO sem mota
            var qSemMota =
                from o in _db.Set<OrdemProducao>().AsNoTracking()
                where o.Estado == ESTADO_EM_PRODUCAO
                join m in _db.Set<Mota>().AsNoTracking() on (int?)o.IDOrdemProducao equals m.IDOrdemProducao into mJoin
                from m in mJoin.DefaultIfEmpty()
                where m == null
                select new { o.IDOrdemProducao, o.NumeroOrdem, o.DataCriacao, idModelo = o.ModeloMotaIDModelo, idCliente = o.ClienteIDCliente };
            if (ordemId.HasValue) qSemMota = qSemMota.Where(o => o.IDOrdemProducao == ordemId.Value);
            var semMota = await qSemMota.ToListAsync();
            foreach (var o in semMota)
                alertas.Add(new AlertaDto { Tipo = "OPERACIONAL", Severidade = "ALTA", Descricao = $"Ordem {o.NumeroOrdem} em produção sem unidade (mota).", OrdemId = o.IDOrdemProducao, ModeloId = o.idModelo, ClienteId = o.idCliente, DataCriacaoIso = o.DataCriacao.ToString("o") });

            // Serviços avaria/garantia em aberto > limiar
            var limite = agora.AddDays(-LIMIAR_DIAS_SERVICO_ABERTO);
            var qServicos =
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                join op in _db.Set<OrdemProducao>().AsNoTracking() on m.IDOrdemProducao equals (int?)op.IDOrdemProducao
                where s.Estado != ESTADO_SERVICO_CONCLUIDO
                      && (s.Tipo == TIPO_SERVICO_AVARIA || s.Tipo == TIPO_SERVICO_GARANTIA)
                      && s.DataServico <= limite
                select new { s.IDServico, s.IDMota, s.Tipo, s.DataServico, op.IDOrdemProducao, idModelo = op.ModeloMotaIDModelo, idCliente = op.ClienteIDCliente };
            if (ordemId.HasValue) qServicos = qServicos.Where(x => x.IDOrdemProducao == ordemId.Value);
            var servicosAbertos = await qServicos.ToListAsync();
            foreach (var s in servicosAbertos)
            {
                var dias = (int)(agora - s.DataServico).TotalDays;
                var tipoNome = s.Tipo == TIPO_SERVICO_AVARIA ? "Avaria" : "Garantia";
                alertas.Add(new AlertaDto { Tipo = "OPERACIONAL", Severidade = s.Tipo == TIPO_SERVICO_AVARIA ? "CRITICA" : "ALTA", Descricao = $"Serviço de {tipoNome} #{s.IDServico} em aberto há {dias} dias.", OrdemId = s.IDOrdemProducao, MotaId = s.IDMota, ServicoId = s.IDServico, ModeloId = s.idModelo, ClienteId = s.idCliente, DataCriacaoIso = s.DataServico.ToString("o") });
            }

            var resultado = alertas.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(tipo))
                resultado = resultado.Where(a => a.Tipo.Equals(tipo.Trim().ToUpper(), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(severidade))
                resultado = resultado.Where(a => a.Severidade.Equals(severidade.Trim().ToUpper(), StringComparison.Ordinal));

            var lista = resultado.ToList();
            return Ok(new { total = lista.Count, alertas = lista });
        }

        // GET /api/controlo-fabrica/expedicao
        [HttpGet("expedicao")]
        public async Task<IActionResult> GetExpedicao()
        {
            var ordens = await _db.Set<OrdemProducao>().AsNoTracking()
                .Where(o => o.Estado == ESTADO_CONCLUIDA)
                .OrderByDescending(o => o.DataConclusao)
                .Select(o => new { o.IDOrdemProducao, o.NumeroOrdem, o.PaisDestino, o.DataCriacao, o.DataConclusao, idModelo = o.ModeloMotaIDModelo, idCliente = o.ClienteIDCliente })
                .ToListAsync();

            if (!ordens.Any())
                return Ok(new { total = 0, ordens = Array.Empty<object>() });

            var ordemIds = ordens.Select(o => o.IDOrdemProducao).ToList();
            var motas = await _db.Set<Mota>().AsNoTracking()
                .Where(m => m.IDOrdemProducao.HasValue && ordemIds.Contains(m.IDOrdemProducao.Value))
                .Select(m => new { m.IDMota, m.IDOrdemProducao, m.NumeroIdentificacao, m.Cor })
                .ToListAsync();

            var clienteIds = ordens.Where(o => o.idCliente.HasValue).Select(o => o.idCliente!.Value).Distinct().ToList();
            var modeloIds = ordens.Where(o => o.idModelo.HasValue).Select(o => o.idModelo!.Value).Distinct().ToList();

            var clientesDict = new Dictionary<int, string>();
            if (clienteIds.Count > 0)
            {
                var cl = await _db.Set<Cliente>().AsNoTracking().Where(c => clienteIds.Contains(c.IDCliente)).Select(c => new { c.IDCliente, c.Nome }).ToListAsync();
                foreach (var c in cl) clientesDict[c.IDCliente] = c.Nome;
            }

            var modelosDict = new Dictionary<int, (string Nome, string Codigo)>();
            if (modeloIds.Count > 0)
            {
                var ml = await _db.Set<ModelosMotum>().AsNoTracking().Where(m => modeloIds.Contains(m.IDModelo)).Select(m => new { m.IDModelo, m.Nome, m.CodigoProduto }).ToListAsync();
                foreach (var m in ml) modelosDict[m.IDModelo] = (m.Nome, m.CodigoProduto);
            }

            var motasPorOrdem = motas.Where(m => m.IDOrdemProducao.HasValue).ToDictionary(m => m.IDOrdemProducao!.Value);

            var resultado = ordens.Select(o =>
            {
                var mota = motasPorOrdem.GetValueOrDefault(o.IDOrdemProducao);
                return new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.PaisDestino,
                    o.DataCriacao,
                    o.DataConclusao,
                    clienteNome = o.idCliente.HasValue ? clientesDict.GetValueOrDefault(o.idCliente.Value) : null,
                    modeloNome = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).Nome : null,
                    modeloCodigo = o.idModelo.HasValue ? modelosDict.GetValueOrDefault(o.idModelo.Value).Codigo : null,
                    motaId = mota?.IDMota,
                    vin = mota?.NumeroIdentificacao,
                    vinPreenchido = mota != null && !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao)
                };
            }).ToList();

            return Ok(new { total = resultado.Count, ordens = resultado });
        }

        // POST /api/controlo-fabrica/expedicao/{id}/marcar-embalada
        [HttpPost("expedicao/{id:int}/marcar-embalada")]
        public async Task<IActionResult> MarcarEmbalada(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_CONCLUIDA)
                return Conflict(new { message = $"Apenas ordens Concluídas podem ser embaladas. Estado: {GetEstadoOrdemNome(ordem.Estado)}." });

            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            if (mota == null)
                return Conflict(new { message = "A ordem não tem unidade associada." });

            var total = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(c => c.IDOrdemProducao == id);
            var ok = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(c => c.IDOrdemProducao == id && c.Incluido == 1);

            return Ok(new
            {
                ordemId = id,
                motaId = mota.IDMota,
                vin = mota.NumeroIdentificacao,
                embalagensCompletas = total > 0 && ok == total,
                totalItensEmbalagem = total,
                itensEmbalagemOk = ok,
                aviso = "Validação de embalagem efetuada. Sem persistência — a BD não tem campo para estado de embalagem."
            });
        }

        // POST /api/controlo-fabrica/expedicao/{id}/marcar-enviada
        [HttpPost("expedicao/{id:int}/marcar-enviada")]
        public async Task<IActionResult> MarcarEnviada(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado != ESTADO_CONCLUIDA)
                return Conflict(new { message = $"Apenas ordens Concluídas podem ser marcadas como enviadas. Estado: {GetEstadoOrdemNome(ordem.Estado)}." });

            var mota = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            if (mota == null)
                return Conflict(new { message = "A ordem não tem unidade associada." });

            if (string.IsNullOrWhiteSpace(mota.NumeroIdentificacao))
                return Conflict(new { message = "O VIN da unidade não está preenchido." });

            if (mota.Estado == ESTADO_MOTA_ATIVA)
                return Ok(new { message = "A unidade já foi marcada como enviada.", ordemId = id, motaId = mota.IDMota, vin = mota.NumeroIdentificacao });

            mota.Estado = ESTADO_MOTA_ATIVA;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Unidade marcada como enviada.", ordemId = id, motaId = mota.IDMota, vin = mota.NumeroIdentificacao, estadoMota = mota.Estado });
        }

        // GET /api/controlo-fabrica/rastreabilidade/{vin}
        [HttpGet("rastreabilidade/{vin}")]
        public async Task<IActionResult> GetRastreabilidade(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin))
                return BadRequest(new { message = "VIN é obrigatório." });

            var vinNorm = vin.Trim().ToUpper();

            var mota = await (
                from m in _db.Set<Mota>().AsNoTracking()
                join mm in _db.Set<ModelosMotum>().AsNoTracking() on m.IDModelo equals mm.IDModelo
                where !string.IsNullOrWhiteSpace(m.NumeroIdentificacao) && m.NumeroIdentificacao!.ToUpper() == vinNorm
                select new
                {
                    m.IDMota,
                    m.NumeroIdentificacao,
                    m.Cor,
                    m.Quilometragem,
                    m.Estado,
                    m.DataRegisto,
                    m.IDOrdemProducao,
                    m.IDModelo,
                    modeloNome = mm.Nome,
                    modeloCodigo = mm.CodigoProduto
                }
            ).FirstOrDefaultAsync();

            if (mota == null)
                return NotFound(new { message = "Nenhuma mota encontrada com esse VIN." });

            object? ordemInfo = null;
            if (mota.IDOrdemProducao.HasValue)
            {
                ordemInfo = await _db.Set<OrdemProducao>().AsNoTracking()
                    .Where(o => o.IDOrdemProducao == mota.IDOrdemProducao.Value)
                    .Select(o => new
                    {
                        o.IDOrdemProducao,
                        o.NumeroOrdem,
                        o.Estado,
                        estadoNome = GetEstadoOrdemNome(o.Estado),
                        o.PaisDestino,
                        o.DataCriacao,
                        o.DataConclusao
                    })
                    .FirstOrDefaultAsync();
            }

            var pecasSn = await _db.Set<MotasPecasSN>().AsNoTracking()
                .Where(x => x.IDMota == mota.IDMota)
                .Join(_db.Set<Peca>().AsNoTracking(), x => x.IDPeca, p => p.IDPeca,
                    (x, p) => new { p.IDPeca, p.PartNumber, p.Descricao, x.NumeroSerie })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            var servicos = await _db.Set<Servico>().AsNoTracking()
                .Where(s => s.IDMota == mota.IDMota)
                .OrderByDescending(s => s.DataServico)
                .Select(s => new { s.IDServico, s.Tipo, s.Descricao, s.Estado, s.DataServico, s.DataConclusao })
                .ToListAsync();

            return Ok(new
            {
                mota = new
                {
                    idMota = mota.IDMota,
                    mota.NumeroIdentificacao,
                    mota.Cor,
                    mota.Quilometragem,
                    mota.Estado,
                    mota.DataRegisto,
                    mota.IDModelo,
                    mota.modeloNome,
                    mota.modeloCodigo
                },
                ordem = ordemInfo,
                pecasSn,
                servicos,
                totalServicos = servicos.Count
            });
        }

        // GET /api/controlo-fabrica/ordens/{id}/controlo-qualidade
        [HttpGet("ordens/{id:int}/controlo-qualidade")]
        public async Task<IActionResult> GetControloQualidade(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            var modeloId = ordem.ModeloMotaIDModelo ?? mota?.IDModelo ?? 0;

            int obrigatorios = 0;
            if (modeloId > 0)
            {
                obrigatorios = await _db.Set<ChecklistModelo>()
                    .AsNoTracking()
                    .Where(cm => cm.IDModelo == modeloId)
                    .Join(_db.Set<Checklist>().AsNoTracking(), cm => cm.IDChecklist, c => c.IDChecklist,
                        (cm, c) => new { c.IDChecklist, c.Tipo })
                    .Distinct()
                    .CountAsync(x => x.Tipo == 3);
            }

            var itens = await _db.Set<ChecklistControlo>()
                .AsNoTracking()
                .Where(x => x.IDOrdemProducao == id)
                .Join(_db.Set<Checklist>().AsNoTracking(), x => x.IDChecklist, c => c.IDChecklist,
                    (x, c) => new { c.IDChecklist, c.Nome, c.Descricao, value = x.ControloFinal })
                .OrderBy(x => x.Nome)
                .ToListAsync();

            var total = itens.Count;
            var feitos = itens.Count(x => x.value == 1);

            return Ok(new
            {
                ordemId = id,
                obrigatorios,
                total,
                feitos,
                ok = obrigatorios == 0 || (total >= obrigatorios && feitos >= obrigatorios),
                itens
            });
        }

        // PUT /api/controlo-fabrica/ordens/{id}/controlo-qualidade/{checklistId}
        [HttpPut("ordens/{id:int}/controlo-qualidade/{checklistId:int}")]
        public async Task<IActionResult> SetControloQualidade(int id, int checklistId, [FromBody] UpdateFlagRequest req)
        {
            if (req == null || (req.Value != 0 && req.Value != 1))
                return BadRequest(new { message = "O valor tem de ser 0 ou 1." });

            var ordemExiste = await _db.Set<OrdemProducao>().AsNoTracking().AnyAsync(o => o.IDOrdemProducao == id);
            if (!ordemExiste)
                return NotFound(new { message = "Ordem não encontrada." });

            var checklist = await _db.Set<Checklist>().AsNoTracking().FirstOrDefaultAsync(c => c.IDChecklist == checklistId);
            if (checklist == null)
                return NotFound(new { message = "Checklist não encontrado." });
            if (checklist.Tipo != 3)
                return BadRequest(new { message = "O checklist indicado não é de controlo final (Tipo 3)." });

            var row = await _db.Set<ChecklistControlo>()
                .FirstOrDefaultAsync(x => x.IDOrdemProducao == id && x.IDChecklist == checklistId);
            if (row == null)
                return BadRequest(new { message = "Checklist de controlo não inicializado para esta ordem. Inicia a ordem primeiro." });

            row.ControloFinal = req.Value;
            await _db.SaveChangesAsync();

            var total = await _db.Set<ChecklistControlo>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id);
            var feitos = await _db.Set<ChecklistControlo>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == id && x.ControloFinal == 1);

            return Ok(new
            {
                message = "Controlo de qualidade atualizado.",
                ordemId = id,
                checklistId,
                value = req.Value,
                resumo = new { total, feitos, ok = total == 0 || feitos == total }
            });
        }

        // GET /api/controlo-fabrica/ordens/{id}/validacao-final
        // Pré-validação sem alterar dados. Indica se a ordem pode ser finalizada.
        [HttpGet("ordens/{id:int}/validacao-final")]
        public async Task<IActionResult> GetValidacaoFinal(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().AsNoTracking().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            var validacao = await BuildValidacaoFinalAsync(id, ordem);
            return Ok(validacao);
        }

        // POST /api/controlo-fabrica/ordens/{id}/finalizar
        // Finalização global — só o controlo de fábrica pode concluir a ordem.
        [HttpPost("ordens/{id:int}/finalizar")]
        public async Task<IActionResult> FinalizarOrdem(int id)
        {
            var ordem = await _db.Set<OrdemProducao>().FirstOrDefaultAsync(o => o.IDOrdemProducao == id);
            if (ordem == null)
                return NotFound(new { message = "Ordem não encontrada." });

            if (ordem.Estado == ESTADO_CONCLUIDA)
                return Conflict(new { message = "A ordem já está concluída." });
            if (ordem.Estado == ESTADO_BLOQUEADA)
                return Conflict(new { message = "A ordem está bloqueada. Desbloqueia antes de finalizar." });
            if (ordem.Estado != ESTADO_EM_PRODUCAO)
                return BadRequest(new { message = $"A ordem tem de estar em produção. Estado atual: {GetEstadoOrdemNome(ordem.Estado)}." });

            var validacao = await BuildValidacaoFinalAsync(id, ordem);
            if (!validacao.PodeFinalizar)
                return BadRequest(new { message = "Não é possível finalizar a ordem.", pendencias = validacao.Pendencias });

            var mota = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDOrdemProducao == id);
            ordem.Estado = ESTADO_CONCLUIDA;
            ordem.DataConclusao = DateTime.UtcNow;
            if (mota != null) mota.Estado = ESTADO_MOTA_ATIVA;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Ordem finalizada com sucesso pelo controlo de fábrica.",
                ordemId = id,
                estado = ordem.Estado,
                estadoNome = GetEstadoOrdemNome(ordem.Estado),
                dataConclusao = ordem.DataConclusao,
                motaId = mota?.IDMota,
                motaEstado = mota?.Estado
            });
        }

        private async Task<ValidacaoFinalResultado> BuildValidacaoFinalAsync(int ordemId, OrdemProducao ordem)
        {
            var mota = await _db.Set<Mota>().AsNoTracking().FirstOrDefaultAsync(m => m.IDOrdemProducao == ordemId);
            var modeloId = ordem.ModeloMotaIDModelo ?? mota?.IDModelo ?? 0;
            var pendencias = new List<string>();
            bool vinOk = false, pecasSnOk = true, montagemOk = true, embalagemOk = true, controloFinalOk = true;

            if (mota == null)
            {
                pendencias.Add("Unidade (mota) não associada à ordem.");
                vinOk = false; pecasSnOk = false; montagemOk = false; embalagemOk = false; controloFinalOk = false;
            }
            else
            {
                vinOk = !string.IsNullOrWhiteSpace(mota.NumeroIdentificacao);
                if (!vinOk) pendencias.Add("VIN / Número de Identificação em falta.");

                if (modeloId > 0)
                {
                    var templates = await _db.Set<ChecklistModelo>()
                        .AsNoTracking()
                        .Where(cm => cm.IDModelo == modeloId)
                        .Join(_db.Set<Checklist>().AsNoTracking(), cm => cm.IDChecklist, c => c.IDChecklist,
                            (cm, c) => new { c.IDChecklist, c.Tipo })
                        .Distinct()
                        .ToListAsync();

                    var reqMontagem = templates.Count(x => x.Tipo == 1);
                    var reqEmbalagem = templates.Count(x => x.Tipo == 2);
                    var reqControlo = templates.Count(x => x.Tipo == 3);

                    if (reqMontagem > 0)
                    {
                        var t = await _db.Set<ChecklistMontagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == ordemId);
                        var f = await _db.Set<ChecklistMontagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == ordemId && x.Verificado == 1);
                        montagemOk = t >= reqMontagem && f >= reqMontagem;
                        if (!montagemOk) pendencias.Add($"Checklist de montagem incompleto ({f}/{reqMontagem} itens).");
                    }

                    if (reqEmbalagem > 0)
                    {
                        var t = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == ordemId);
                        var f = await _db.Set<ChecklistEmbalagem>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == ordemId && x.Incluido == 1);
                        embalagemOk = t >= reqEmbalagem && f >= reqEmbalagem;
                        if (!embalagemOk) pendencias.Add($"Checklist de embalagem incompleto ({f}/{reqEmbalagem} itens).");
                    }

                    if (reqControlo > 0)
                    {
                        var t = await _db.Set<ChecklistControlo>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == ordemId);
                        var f = await _db.Set<ChecklistControlo>().AsNoTracking().CountAsync(x => x.IDOrdemProducao == ordemId && x.ControloFinal == 1);
                        controloFinalOk = t >= reqControlo && f >= reqControlo;
                        if (!controloFinalOk) pendencias.Add($"Checklist de controlo final incompleto ({f}/{reqControlo} itens).");
                    }

                    var pecasObrig = await _db.Set<ModeloPecasSN>().AsNoTracking()
                        .Where(x => x.IDModelo == modeloId).Select(x => x.IDPeca).Distinct().CountAsync();
                    if (pecasObrig > 0)
                    {
                        var preench = await _db.Set<MotasPecasSN>().AsNoTracking()
                            .Where(x => x.IDMota == mota.IDMota && !string.IsNullOrWhiteSpace(x.NumeroSerie))
                            .Select(x => x.IDPeca).Distinct().CountAsync();
                        pecasSnOk = preench >= pecasObrig;
                        if (!pecasSnOk) pendencias.Add($"Peças serializadas por preencher ({preench}/{pecasObrig}).");
                    }
                }
                else
                {
                    pendencias.Add("Não foi possível determinar o modelo da ordem/unidade.");
                    pecasSnOk = false;
                    montagemOk = false;
                    embalagemOk = false;
                    controloFinalOk = false;
                }
            }

            return new ValidacaoFinalResultado
            {
                OrdemId = ordemId,
                PodeFinalizar = !pendencias.Any(),
                VinOk = vinOk,
                PecasSnOk = pecasSnOk,
                MontagemOk = montagemOk,
                EmbalagemOk = embalagemOk,
                ControloFinalOk = controloFinalOk,
                Pendencias = pendencias
            };
        }

        // GET /api/controlo-fabrica/servicos?estado=1&emAberto=true
        [HttpGet("servicos")]
        public async Task<IActionResult> GetServicos([FromQuery] int? estado = null, [FromQuery] int? motaId = null, [FromQuery] bool? emAberto = null)
        {
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
                    m.NumeroIdentificacao,
                    m.IDOrdemProducao,
                    modeloNome = mm.Nome
                };

            if (estado.HasValue) query = query.Where(x => x.Estado == estado.Value);
            if (motaId.HasValue) query = query.Where(x => x.IDMota == motaId.Value);
            if (emAberto == true) query = query.Where(x => x.Estado != ESTADO_SERVICO_CONCLUIDO);

            var lista = await query.OrderByDescending(x => x.DataServico).ToListAsync();
            return Ok(new { total = lista.Count, servicos = lista });
        }

        // GET /api/controlo-fabrica/utilizadores?estado=1
        [HttpGet("utilizadores")]
        public async Task<IActionResult> GetEquipa([FromQuery] int? estado = null)
        {
            var query = _db.Set<Utilizadore>().AsNoTracking();
            if (estado.HasValue)
                query = query.Where(u => u.Estado == estado.Value);

            var users = await query.OrderBy(u => u.Nome)
                .Select(u => new { u.IdUtilizador, u.Nome, u.Email, u.Telefone, u.Estado })
                .ToListAsync();

            var totalAtivos = await _db.Set<UtilizadorMotum>().AsNoTracking().CountAsync(x => x.Estado == ESTADO_UTILIZADOR_ATIVO);

            return Ok(new { totalAtivos, total = users.Count, utilizadores = users });
        }

        private static bool EstadoOrdemValido(int estado) =>
            estado == ESTADO_ABERTA || estado == ESTADO_EM_PRODUCAO || estado == ESTADO_CONCLUIDA || estado == ESTADO_BLOQUEADA;

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
