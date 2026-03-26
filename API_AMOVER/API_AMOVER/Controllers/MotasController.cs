using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class CreateMotaRequest
    {
        public int IDModelo { get; set; } // pode vir 0 e usar o modelo da ordem
        public string Cor { get; set; } = "";
        public double Quilometragem { get; set; }
        public int Estado { get; set; } = 0; // 0=EmProdução
        public int IDOrdemProducao { get; set; }
        public string NumeroIdentificacao { get; set; } = ""; // VIN / quadro (pode vir vazio)
    }

    public class UpdateNumeroIdentificacaoRequest
    {
        public string NumeroIdentificacao { get; set; } = "";
    }

    public class AddPecaSnRequest
    {
        public int IDPeca { get; set; }
        public string NumeroSerie { get; set; } = "";
    }

    public class UpdateMotaEstadoRequest
    {
        public int Estado { get; set; }
    }

    public class PecaSnResumoItemDto
    {
        public int IDPeca { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public bool Preenchida { get; set; }
        public string NumeroSerie { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/motas")]
    public class MotasController : ControllerBase
    {
        private readonly LcmContext _db;

        private const int ESTADO_EM_PRODUCAO = 0;
        private const int ESTADO_ATIVO = 1;
        private const int ESTADO_EM_MANUTENCAO = 2;
        private const int ESTADO_DESCONTINUADO = 3;

        public MotasController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/motas?estado=1&ordemId=10&semVin=true
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? estado = null,
            [FromQuery] int? ordemId = null,
            [FromQuery] bool? semVin = null)
        {
            if (estado.HasValue && !EstadoValido(estado.Value))
                return BadRequest(new { message = "Estado inválido para mota." });

            if (ordemId.HasValue && ordemId.Value <= 0)
                return BadRequest(new { message = "ordemId inválido." });

            var query = _db.Set<Mota>().AsNoTracking();

            if (estado.HasValue)
                query = query.Where(m => m.Estado == estado.Value);

            if (ordemId.HasValue)
                query = query.Where(m => m.IDOrdemProducao == ordemId.Value);

            if (semVin.HasValue)
            {
                if (semVin.Value)
                    query = query.Where(m => string.IsNullOrWhiteSpace(m.NumeroIdentificacao));
                else
                    query = query.Where(m => !string.IsNullOrWhiteSpace(m.NumeroIdentificacao));
            }

            var list = await query
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

            return Ok(list);
        }

        // GET /api/motas/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(x => x.IDMota == id)
                .Select(x => new
                {
                    x.IDMota,
                    x.IDModelo,
                    x.IDOrdemProducao,
                    x.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(x.NumeroIdentificacao),
                    x.Cor,
                    x.Quilometragem,
                    x.Estado,
                    x.DataRegisto
                })
                .FirstOrDefaultAsync();

            return mota == null
                ? NotFound(new { message = "Mota não encontrada." })
                : Ok(mota);
        }

        // GET /api/motas/by-vin/{vin}
        [HttpGet("by-vin/{vin}")]
        public async Task<IActionResult> GetByVin(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin))
                return BadRequest(new { message = "VIN / Número de Identificação é obrigatório." });

            var numero = NormalizeNumeroIdentificacao(vin);

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.NumeroIdentificacao) &&
                    x.NumeroIdentificacao!.ToUpper() == numero)
                .Select(x => new
                {
                    x.IDMota,
                    x.IDModelo,
                    x.IDOrdemProducao,
                    x.NumeroIdentificacao,
                    vinPreenchido = !string.IsNullOrWhiteSpace(x.NumeroIdentificacao),
                    x.Cor,
                    x.Quilometragem,
                    x.Estado,
                    x.DataRegisto
                })
                .FirstOrDefaultAsync();

            return mota == null
                ? NotFound(new { message = "Nenhuma mota encontrada com esse VIN / Número de Identificação." })
                : Ok(mota);
        }

        // POST /api/motas
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMotaRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.IDOrdemProducao <= 0)
                return BadRequest(new { message = "IDOrdemProducao é obrigatório." });

            if (!EstadoValido(req.Estado))
                return BadRequest(new { message = "Estado inválido para mota." });

            if (req.Quilometragem < 0)
                return BadRequest(new { message = "Quilometragem inválida." });

            var ordem = await _db.Set<OrdemProducao>()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.IDOrdemProducao == req.IDOrdemProducao);

            if (ordem == null)
                return NotFound(new { message = "Ordem de Produção não encontrada." });

            var motaJaExisteNaOrdem = await _db.Set<Mota>()
                .AsNoTracking()
                .AnyAsync(m => m.IDOrdemProducao == req.IDOrdemProducao);

            if (motaJaExisteNaOrdem)
                return Conflict(new { message = "Já existe uma mota associada a esta Ordem de Produção." });

            var modeloDaOrdem = ordem.ModeloMotaIDModelo ?? 0;
            var modeloFinal = req.IDModelo > 0 ? req.IDModelo : modeloDaOrdem;

            if (modeloFinal <= 0)
                return BadRequest(new { message = "IDModelo é obrigatório ou tem de estar definido na ordem." });

            if (modeloDaOrdem > 0 && req.IDModelo > 0 && req.IDModelo != modeloDaOrdem)
                return BadRequest(new { message = "O IDModelo enviado não corresponde ao modelo associado à ordem." });

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
                IDModelo = modeloFinal,
                IDOrdemProducao = req.IDOrdemProducao,
                NumeroIdentificacao = numeroIdentificacao,
                Cor = string.IsNullOrWhiteSpace(req.Cor) ? "N/A" : req.Cor.Trim(),
                Quilometragem = req.Quilometragem,
                Estado = req.Estado,
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

        // PUT /api/motas/{id}/identificacao
        [HttpPut("{id:int}/identificacao")]
        public async Task<IActionResult> UpdateNumeroIdentificacao(int id, [FromBody] UpdateNumeroIdentificacaoRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.NumeroIdentificacao))
                return BadRequest(new { message = "O Número de Identificação / VIN é obrigatório." });

            var mota = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDMota == id);
            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var numero = NormalizeNumeroIdentificacao(req.NumeroIdentificacao);

            var vinDuplicado = await _db.Set<Mota>()
                .AsNoTracking()
                .AnyAsync(m =>
                    m.IDMota != id &&
                    !string.IsNullOrWhiteSpace(m.NumeroIdentificacao) &&
                    m.NumeroIdentificacao!.ToUpper() == numero);

            if (vinDuplicado)
                return Conflict(new { message = "Já existe outra mota com esse VIN / Número de Identificação." });

            mota.NumeroIdentificacao = numero;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Número de Identificação atualizado com sucesso.",
                mota.IDMota,
                mota.NumeroIdentificacao
            });
        }

        // GET /api/motas/{id}/pecas-sn
        [HttpGet("{id:int}/pecas-sn")]
        public async Task<IActionResult> GetPecasSn(int id)
        {
            var motaExiste = await _db.Set<Mota>()
                .AsNoTracking()
                .AnyAsync(m => m.IDMota == id);

            if (!motaExiste)
                return NotFound(new { message = "Mota não encontrada." });

            var list = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDMota == id)
                .Join(
                    _db.Set<Peca>().AsNoTracking(),
                    x => x.IDPeca,
                    p => p.IDPeca,
                    (x, p) => new
                    {
                        x.IDMotasPecasSN,
                        x.IDMota,
                        x.IDPeca,
                        p.PartNumber,
                        p.Descricao,
                        x.NumeroSerie
                    })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            return Ok(list);
        }

        // GET /api/motas/{id}/pecas-sn/resumo
        [HttpGet("{id:int}/pecas-sn/resumo")]
        public async Task<IActionResult> GetPecasSnResumo(int id)
        {
            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDMota == id);

            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var resumo = await BuildPecasSnResumoAsync(mota);
            return Ok(resumo);
        }

        // POST /api/motas/{id}/pecas-sn
        [HttpPost("{id:int}/pecas-sn")]
        public async Task<IActionResult> AddOrUpdatePecaSn(int id, [FromBody] AddPecaSnRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.IDPeca <= 0 || string.IsNullOrWhiteSpace(req.NumeroSerie))
                return BadRequest(new { message = "IDPeca e NumeroSerie são obrigatórios." });

            var mota = await _db.Set<Mota>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IDMota == id);

            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            var peca = await _db.Set<Peca>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IDPeca == req.IDPeca);

            if (peca == null)
                return NotFound(new { message = "Peça não encontrada." });

            var pecaPertenceAoModelo = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .AnyAsync(x => x.IDModelo == mota.IDModelo && x.IDPeca == req.IDPeca);

            if (!pecaPertenceAoModelo)
            {
                return BadRequest(new
                {
                    message = "A peça indicada não está configurada como peça serializada relevante para o modelo desta mota."
                });
            }

            var numeroSerie = req.NumeroSerie.Trim();

            var numeroSerieDuplicado = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IDPeca == req.IDPeca &&
                    x.IDMota != id &&
                    x.NumeroSerie != null &&
                    x.NumeroSerie == numeroSerie);

            if (numeroSerieDuplicado)
            {
                return Conflict(new
                {
                    message = "Já existe outra mota com esse número de série para esta peça."
                });
            }

            var row = await _db.Set<MotasPecasSN>()
                .FirstOrDefaultAsync(x => x.IDMota == id && x.IDPeca == req.IDPeca);

            bool created;

            if (row == null)
            {
                row = new MotasPecasSN
                {
                    IDMota = id,
                    IDPeca = req.IDPeca,
                    NumeroSerie = numeroSerie
                };

                _db.Set<MotasPecasSN>().Add(row);
                created = true;
            }
            else
            {
                row.NumeroSerie = numeroSerie;
                created = false;
            }

            await _db.SaveChangesAsync();

            var resumo = await BuildPecasSnResumoAsync(mota);

            return Ok(new
            {
                row.IDMotasPecasSN,
                created,
                resumo
            });
        }

        // DELETE /api/motas/pecas-sn/{idMotaPecaSn}
        [HttpDelete("pecas-sn/{idMotaPecaSn:int}")]
        public async Task<IActionResult> DeletePecaSn(int idMotaPecaSn)
        {
            var row = await _db.Set<MotasPecasSN>()
                .FirstOrDefaultAsync(x => x.IDMotasPecasSN == idMotaPecaSn);

            if (row == null)
                return NotFound(new { message = "Registo de peça serializada não encontrado." });

            var usadaEmServico = await _db.Set<ServicosPecasAlterada>()
                .AsNoTracking()
                .AnyAsync(x => x.IDMotasPecasSN == idMotaPecaSn);

            if (usadaEmServico)
            {
                return Conflict(new
                {
                    message = "Não é possível remover esta peça serializada porque já está referenciada em histórico de serviço."
                });
            }

            _db.Set<MotasPecasSN>().Remove(row);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        // PUT /api/motas/{id}/estado
        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> UpdateEstado(int id, [FromBody] UpdateMotaEstadoRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (!EstadoValido(req.Estado))
                return BadRequest(new { message = "Estado inválido para mota." });

            var mota = await _db.Set<Mota>().FirstOrDefaultAsync(m => m.IDMota == id);
            if (mota == null)
                return NotFound(new { message = "Mota não encontrada." });

            mota.Estado = req.Estado;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Estado da mota atualizado com sucesso.",
                mota.IDMota,
                mota.Estado
            });
        }

        private static bool EstadoValido(int estado)
        {
            return estado == ESTADO_EM_PRODUCAO ||
                   estado == ESTADO_ATIVO ||
                   estado == ESTADO_EM_MANUTENCAO ||
                   estado == ESTADO_DESCONTINUADO;
        }

        private static string NormalizeNumeroIdentificacao(string value)
        {
            return (value ?? string.Empty).Trim().ToUpper();
        }

        private static string NormalizeNumeroIdentificacaoOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeNumeroIdentificacao(value);
        }

        private async Task<object> BuildPecasSnResumoAsync(Mota mota)
        {
            var obrigatorias = await _db.Set<ModeloPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDModelo == mota.IDModelo)
                .Join(
                    _db.Set<Peca>().AsNoTracking(),
                    x => x.IDPeca,
                    p => p.IDPeca,
                    (x, p) => new
                    {
                        p.IDPeca,
                        p.PartNumber,
                        p.Descricao
                    })
                .OrderBy(x => x.PartNumber)
                .ToListAsync();

            var registadas = await _db.Set<MotasPecasSN>()
                .AsNoTracking()
                .Where(x => x.IDMota == mota.IDMota)
                .ToListAsync();

            var registadasMap = registadas
                .GroupBy(x => x.IDPeca)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().NumeroSerie ?? string.Empty
                );

            var itens = obrigatorias.Select(p => new PecaSnResumoItemDto
            {
                IDPeca = p.IDPeca,
                PartNumber = p.PartNumber,
                Descricao = p.Descricao,
                Preenchida = registadasMap.ContainsKey(p.IDPeca) && !string.IsNullOrWhiteSpace(registadasMap[p.IDPeca]),
                NumeroSerie = registadasMap.ContainsKey(p.IDPeca) ? registadasMap[p.IDPeca] : string.Empty
            }).ToList();

            var totalObrigatorias = itens.Count;
            var preenchidas = itens.Count(x => x.Preenchida);

            return new
            {
                motaId = mota.IDMota,
                idModelo = mota.IDModelo,
                totalObrigatorias,
                preenchidas,
                ok = totalObrigatorias == 0 || preenchidas == totalObrigatorias,
                pecas = itens
            };
        }
    }
}