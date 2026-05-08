using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_ORDEM_EM_PRODUCAO = 1;
        private const int ESTADO_ORDEM_BLOQUEADA = 3;
        private const int ESTADO_SERVICO_CONCLUIDO = 2;
        private const int ESTADO_UTILIZADOR_ATIVO = 1;

        public DashboardController(LcmContext db) => _db = db;

        // GET /api/dashboard/resumo
        [HttpGet("resumo")]
        public async Task<IActionResult> GetResumo()
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

            // Orders with at least one controlo item pending (ControloFinal = 0)
            var ordemIdsControloPendente = await _db.Set<ChecklistControlo>().AsNoTracking()
                .Where(c => c.ControloFinal == 0)
                .Select(c => c.IDOrdemProducao)
                .Distinct()
                .ToListAsync();
            var pendentesControloSet = ordemIdsControloPendente.ToHashSet();

            var equipaAtiva = await _db.Set<UtilizadorMotum>().AsNoTracking()
                .CountAsync(u => u.Estado == ESTADO_UTILIZADOR_ATIVO);

            var servicosEmAberto = await _db.Set<Servico>().AsNoTracking()
                .CountAsync(s => s.Estado != ESTADO_SERVICO_CONCLUIDO);

            // Batch-fetch names
            var clienteIds = ordens.Where(o => o.idCliente.HasValue).Select(o => o.idCliente!.Value).Distinct().ToList();
            var modeloIds = ordens.Where(o => o.idModelo.HasValue).Select(o => o.idModelo!.Value).Distinct().ToList();

            var clientesDict = new Dictionary<int, string>();
            if (clienteIds.Count > 0)
            {
                var cl = await _db.Set<Cliente>().AsNoTracking()
                    .Where(c => clienteIds.Contains(c.IDCliente))
                    .Select(c => new { c.IDCliente, c.Nome })
                    .ToListAsync();
                foreach (var c in cl) clientesDict[c.IDCliente] = c.Nome;
            }

            var modelosDict = new Dictionary<int, string>();
            if (modeloIds.Count > 0)
            {
                var ml = await _db.Set<ModelosMotum>().AsNoTracking()
                    .Where(m => modeloIds.Contains(m.IDModelo))
                    .Select(m => new { m.IDModelo, m.Nome })
                    .ToListAsync();
                foreach (var m in ml) modelosDict[m.IDModelo] = m.Nome;
            }

            // In-memory derived sets
            var motasPorOrdem = motas.ToDictionary(m => m.IDOrdemProducao);
            var ordemIdsComMota = motasPorOrdem.Keys.ToHashSet();

            // Per-order list
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

            // Aggregate metrics
            var totalOrdens = ordens.Count;
            var emProducao = ordens.Count(o => o.Estado == ESTADO_ORDEM_EM_PRODUCAO);
            var bloqueadas = ordens.Count(o => o.Estado == ESTADO_ORDEM_BLOQUEADA);
            var semUnidade = ordens.Count(o => o.Estado == ESTADO_ORDEM_EM_PRODUCAO && !ordemIdsComMota.Contains(o.IDOrdemProducao));
            var controloPendente = pendentesControloSet.Count;
            var vinPendente = motas.Count(m => string.IsNullOrWhiteSpace(m.NumeroIdentificacao));

            return Ok(new
            {
                totalOrdens,
                emProducao,
                bloqueadas,
                semUnidade,
                controloPendente,
                vinPendente,
                equipaAtiva,
                servicosEmAberto,
                ordens = ordensResumo
            });
        }

        private static string GetEstadoOrdemNome(int estado) => estado switch
        {
            0 => "Aberta",
            1 => "Em Produção",
            2 => "Concluída",
            3 => "Bloqueada",
            _ => "Desconhecido"
        };
    }
}
