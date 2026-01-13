using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
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
        public EncomendasController(LcmContext db) => _db = db;

        // GET /api/encomendas?clienteId=1&estado=2
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? clienteId = null, [FromQuery] int? estado = null)
        {
            var q = _db.Set<Encomenda>().AsNoTracking();

            if (clienteId.HasValue) q = q.Where(e => e.IDCliente == clienteId.Value);
            if (estado.HasValue) q = q.Where(e => e.Estado == estado.Value);

            var lista = await q
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
            var e = await _db.Set<Encomenda>()
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

            return e == null ? NotFound() : Ok(e);
        }

        // POST /api/encomendas
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EncomendaCreateRequest req)
        {
            if (req.IDCliente <= 0 || req.IDModelo <= 0 || req.Quantidade <= 0)
                return BadRequest(new { message = "IDCliente, IDModelo e Quantidade são obrigatórios." });

            var e = new Encomenda
            {
                IDCliente = req.IDCliente,
                IDModelo = req.IDModelo,
                Quantidade = req.Quantidade,
                Estado = req.Estado,
                DateCriacao = DateTime.UtcNow,
                DataEntrega = req.DataEntrega
            };

            _db.Set<Encomenda>().Add(e);
            await _db.SaveChangesAsync();

            // (opcional) atualizar UltimaEncomenda do cliente
            var cliente = await _db.Set<Cliente>().FirstOrDefaultAsync(c => c.IDCliente == req.IDCliente);
            if (cliente != null)
            {
                cliente.UltimaEncomenda = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { e.IDEncomenda });
        }

        // PUT /api/encomendas/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] EncomendaCreateRequest req)
        {
            var e = await _db.Set<Encomenda>().FirstOrDefaultAsync(x => x.IDEncomenda == id);
            if (e == null) return NotFound();

            if (req.Quantidade <= 0) return BadRequest(new { message = "Quantidade inválida." });

            e.IDCliente = req.IDCliente;
            e.IDModelo = req.IDModelo;
            e.Quantidade = req.Quantidade;
            e.Estado = req.Estado;
            e.DataEntrega = req.DataEntrega;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
