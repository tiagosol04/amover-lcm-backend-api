using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class AlertaDto
    {
        public string Tipo { get; set; } = string.Empty;
        public string Severidade { get; set; } = string.Empty;
        public string Estado { get; set; } = "ABERTO";
        public string Origem { get; set; } = "FABRICA";
        public string Descricao { get; set; } = string.Empty;
        public int? OrdemId { get; set; }
        public int? MotaId { get; set; }
        public int? ServicoId { get; set; }
        public int? ModeloId { get; set; }
        public int? ClienteId { get; set; }
        public string DataCriacaoIso { get; set; } = string.Empty;
        public bool Calculado { get; set; } = true;
    }

    [ApiController]
    [Route("api/alertas")]
    public class AlertasController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_ORDEM_EM_PRODUCAO = 1;
        private const int ESTADO_ORDEM_BLOQUEADA = 3;
        private const int ESTADO_SERVICO_CONCLUIDO = 2;
        private const int TIPO_SERVICO_AVARIA = 2;
        private const int TIPO_SERVICO_GARANTIA = 3;
        private const int LIMIAR_DIAS_SERVICO_ABERTO = 7;

        public AlertasController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/alertas?ordemId=X&tipo=BLOQUEIO&severidade=CRITICA
        [HttpGet]
        public async Task<IActionResult> GetAlertas(
            [FromQuery] int? ordemId = null,
            [FromQuery] string? tipo = null,
            [FromQuery] string? severidade = null)
        {
            var alertas = new List<AlertaDto>();
            var agora = DateTime.UtcNow;

            // --- 1. Ordens BLOQUEADAS → BLOQUEIO / CRITICA ---
            var queryBloqueadas = _db.Set<OrdemProducao>().AsNoTracking()
                .Where(o => o.Estado == ESTADO_ORDEM_BLOQUEADA);

            if (ordemId.HasValue)
                queryBloqueadas = queryBloqueadas.Where(o => o.IDOrdemProducao == ordemId.Value);

            var ordensBloqueadas = await queryBloqueadas
                .Select(o => new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.DataCriacao,
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                })
                .ToListAsync();

            foreach (var o in ordensBloqueadas)
            {
                alertas.Add(new AlertaDto
                {
                    Tipo = "BLOQUEIO",
                    Severidade = "CRITICA",
                    Descricao = $"Ordem {o.NumeroOrdem} está bloqueada e não avança em produção.",
                    OrdemId = o.IDOrdemProducao,
                    ModeloId = o.idModelo,
                    ClienteId = o.idCliente,
                    DataCriacaoIso = o.DataCriacao.ToString("o")
                });
            }

            // --- 2. Ordens EM_PRODUCAO sem mota associada → OPERACIONAL / ALTA ---
            var queryOrdensSemMota =
                from o in _db.Set<OrdemProducao>().AsNoTracking()
                where o.Estado == ESTADO_ORDEM_EM_PRODUCAO
                join m in _db.Set<Mota>().AsNoTracking()
                    on (int?)o.IDOrdemProducao equals m.IDOrdemProducao into mJoin
                from m in mJoin.DefaultIfEmpty()
                where m == null
                select new
                {
                    o.IDOrdemProducao,
                    o.NumeroOrdem,
                    o.DataCriacao,
                    idModelo = o.ModeloMotaIDModelo,
                    idCliente = o.ClienteIDCliente
                };

            if (ordemId.HasValue)
                queryOrdensSemMota = queryOrdensSemMota.Where(o => o.IDOrdemProducao == ordemId.Value);

            var ordensSemMota = await queryOrdensSemMota.ToListAsync();

            foreach (var o in ordensSemMota)
            {
                alertas.Add(new AlertaDto
                {
                    Tipo = "OPERACIONAL",
                    Severidade = "ALTA",
                    Descricao = $"Ordem {o.NumeroOrdem} está em produção mas não tem unidade (mota) associada.",
                    OrdemId = o.IDOrdemProducao,
                    ModeloId = o.idModelo,
                    ClienteId = o.idCliente,
                    DataCriacaoIso = o.DataCriacao.ToString("o")
                });
            }

            // --- 3. Serviços AVARIA/GARANTIA em aberto há mais de LIMIAR_DIAS_SERVICO_ABERTO dias → OPERACIONAL ---
            var limite = agora.AddDays(-LIMIAR_DIAS_SERVICO_ABERTO);

            var queryServicos =
                from s in _db.Set<Servico>().AsNoTracking()
                join m in _db.Set<Mota>().AsNoTracking() on s.IDMota equals m.IDMota
                join op in _db.Set<OrdemProducao>().AsNoTracking() on m.IDOrdemProducao equals (int?)op.IDOrdemProducao
                where s.Estado != ESTADO_SERVICO_CONCLUIDO
                      && (s.Tipo == TIPO_SERVICO_AVARIA || s.Tipo == TIPO_SERVICO_GARANTIA)
                      && s.DataServico <= limite
                select new
                {
                    s.IDServico,
                    s.IDMota,
                    s.Tipo,
                    s.DataServico,
                    op.IDOrdemProducao,
                    idModelo = op.ModeloMotaIDModelo,
                    idCliente = op.ClienteIDCliente
                };

            if (ordemId.HasValue)
                queryServicos = queryServicos.Where(x => x.IDOrdemProducao == ordemId.Value);

            var servicosAbertos = await queryServicos.ToListAsync();

            foreach (var s in servicosAbertos)
            {
                var diasAberto = (int)(agora - s.DataServico).TotalDays;
                var tipoNome = s.Tipo == TIPO_SERVICO_AVARIA ? "Avaria" : "Garantia";

                alertas.Add(new AlertaDto
                {
                    Tipo = "OPERACIONAL",
                    Severidade = s.Tipo == TIPO_SERVICO_AVARIA ? "CRITICA" : "ALTA",
                    Descricao = $"Serviço de {tipoNome} #{s.IDServico} em aberto há {diasAberto} dias.",
                    OrdemId = s.IDOrdemProducao,
                    MotaId = s.IDMota,
                    ServicoId = s.IDServico,
                    ModeloId = s.idModelo,
                    ClienteId = s.idCliente,
                    DataCriacaoIso = s.DataServico.ToString("o")
                });
            }

            // --- Aplicar filtros opcionais ---
            var resultado = alertas.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(tipo))
                resultado = resultado.Where(a => a.Tipo.Equals(tipo.Trim().ToUpper(), StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(severidade))
                resultado = resultado.Where(a => a.Severidade.Equals(severidade.Trim().ToUpper(), StringComparison.Ordinal));

            var lista = resultado.ToList();

            return Ok(new
            {
                total = lista.Count,
                alertas = lista
            });
        }
    }
}
