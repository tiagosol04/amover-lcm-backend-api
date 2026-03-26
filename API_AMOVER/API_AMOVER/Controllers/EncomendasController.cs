using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class EncomendaCreateRequest
    {
        public int IDCliente { get; set; }
        public int IDModelo { get; set; }
        public int Quantidade { get; set; }
        public int Estado { get; set; }
        public DateTime? DataEntrega { get; set; }
    }

    [ApiController]
    [Route("api/encomendas")]
    public class EncomendasController : ControllerBase
    {
        private readonly LcmContext _db;

        public EncomendasController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/encomendas?clienteId=1&estado=2
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? clienteId = null, [FromQuery] int? estado = null)
        {
            if (clienteId.HasValue && clienteId.Value <= 0)
                return BadRequest(new { message = "clienteId inválido." });

            if (estado.HasValue && estado.Value < 0)
                return BadRequest(new { message = "estado inválido." });

            var query = _db.Set<Encomenda>().AsNoTracking();

            if (clienteId.HasValue)
                query = query.Where(e => e.IDCliente == clienteId.Value);

            if (estado.HasValue)
                query = query.Where(e => e.Estado == estado.Value);

            var lista = await query
                .OrderByDescending(e => e.DateCriacao)
                .Select(e => new
                {
                    e.IDEncomenda,
                    e.IDCliente,
                    e.IDModelo,
                    e.Quantidade,
                    e.Estado,
                    e.DateCriacao,
                    e.DataEntrega
                })
                .ToListAsync();

            return Ok(lista);
        }

        // GET /api/encomendas/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var encomenda = await _db.Set<Encomenda>()
                .AsNoTracking()
                .Where(x => x.IDEncomenda == id)
                .Select(x => new
                {
                    x.IDEncomenda,
                    x.IDCliente,
                    x.IDModelo,
                    x.Quantidade,
                    x.Estado,
                    x.DateCriacao,
                    x.DataEntrega
                })
                .FirstOrDefaultAsync();

            if (encomenda == null)
                return NotFound(new { message = "Encomenda não encontrada." });

            return Ok(encomenda);
        }

        // POST /api/encomendas
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EncomendaCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (req.IDCliente <= 0 || req.IDModelo <= 0 || req.Quantidade <= 0)
                return BadRequest(new { message = "IDCliente, IDModelo e Quantidade são obrigatórios." });

            if (req.Estado < 0)
                return BadRequest(new { message = "Estado inválido." });

            var cliente = await _db.Set<Cliente>()
                .FirstOrDefaultAsync(c => c.IDCliente == req.IDCliente);

            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            var modeloExiste = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .AnyAsync(m => m.IDModelo == req.IDModelo);

            if (!modeloExiste)
                return NotFound(new { message = "Modelo não encontrado." });

            var agora = DateTime.UtcNow;

            var encomenda = new Encomenda
            {
                IDCliente = req.IDCliente,
                IDModelo = req.IDModelo,
                Quantidade = req.Quantidade,
                Estado = req.Estado,
                DateCriacao = agora,
                DataEntrega = req.DataEntrega
            };

            _db.Set<Encomenda>().Add(encomenda);

            cliente.UltimaEncomenda = agora;

            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = encomenda.IDEncomenda }, new { encomenda.IDEncomenda });
        }

        // PUT /api/encomendas/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] EncomendaCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            var encomenda = await _db.Set<Encomenda>()
                .FirstOrDefaultAsync(x => x.IDEncomenda == id);

            if (encomenda == null)
                return NotFound(new { message = "Encomenda não encontrada." });

            if (req.IDCliente <= 0 || req.IDModelo <= 0 || req.Quantidade <= 0)
                return BadRequest(new { message = "IDCliente, IDModelo e Quantidade são obrigatórios." });

            if (req.Estado < 0)
                return BadRequest(new { message = "Estado inválido." });

            var clienteExiste = await _db.Set<Cliente>()
                .AsNoTracking()
                .AnyAsync(c => c.IDCliente == req.IDCliente);

            if (!clienteExiste)
                return NotFound(new { message = "Cliente não encontrado." });

            var modeloExiste = await _db.Set<ModelosMotum>()
                .AsNoTracking()
                .AnyAsync(m => m.IDModelo == req.IDModelo);

            if (!modeloExiste)
                return NotFound(new { message = "Modelo não encontrado." });

            encomenda.IDCliente = req.IDCliente;
            encomenda.IDModelo = req.IDModelo;
            encomenda.Quantidade = req.Quantidade;
            encomenda.Estado = req.Estado;
            encomenda.DataEntrega = req.DataEntrega;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}