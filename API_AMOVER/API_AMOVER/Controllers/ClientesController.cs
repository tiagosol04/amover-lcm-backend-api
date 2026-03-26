using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Controllers
{
    public class ClienteCreateRequest
    {
        public string Nome { get; set; } = string.Empty;
        public int Tipo { get; set; }
    }

    public class ClienteUpdateRequest
    {
        public string Nome { get; set; } = string.Empty;
        public int Tipo { get; set; }
    }

    [ApiController]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly LcmContext _db;

        public ClientesController(LcmContext db)
        {
            _db = db;
        }

        // GET /api/clientes
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var clientes = await _db.Set<Cliente>()
                .AsNoTracking()
                .OrderByDescending(c => c.DataCriacao)
                .Select(c => new
                {
                    c.IDCliente,
                    c.Nome,
                    c.Tipo,
                    c.DataCriacao,
                    c.DataModificacao,
                    c.UltimaEncomenda
                })
                .ToListAsync();

            return Ok(clientes);
        }

        // GET /api/clientes/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var cliente = await _db.Set<Cliente>()
                .AsNoTracking()
                .Where(x => x.IDCliente == id)
                .Select(x => new
                {
                    x.IDCliente,
                    x.Nome,
                    x.Tipo,
                    x.DataCriacao,
                    x.DataModificacao,
                    x.UltimaEncomenda
                })
                .FirstOrDefaultAsync();

            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            return Ok(cliente);
        }

        // POST /api/clientes
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ClienteCreateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            if (string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "Nome é obrigatório." });

            if (req.Tipo < 0)
                return BadRequest(new { message = "Tipo inválido." });

            var nome = req.Nome.Trim();

            var duplicado = await _db.Set<Cliente>()
                .AsNoTracking()
                .AnyAsync(c => c.Nome != null && c.Nome.ToUpper() == nome.ToUpper());

            if (duplicado)
                return Conflict(new { message = "Já existe um cliente com esse nome." });

            var cliente = new Cliente
            {
                Nome = nome,
                Tipo = req.Tipo,
                DataCriacao = DateTime.UtcNow
            };

            _db.Set<Cliente>().Add(cliente);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = cliente.IDCliente }, new { cliente.IDCliente });
        }

        // PUT /api/clientes/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ClienteUpdateRequest req)
        {
            if (req == null)
                return BadRequest(new { message = "Body inválido." });

            var cliente = await _db.Set<Cliente>()
                .FirstOrDefaultAsync(c => c.IDCliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente não encontrado." });

            if (string.IsNullOrWhiteSpace(req.Nome))
                return BadRequest(new { message = "Nome é obrigatório." });

            if (req.Tipo < 0)
                return BadRequest(new { message = "Tipo inválido." });

            var nome = req.Nome.Trim();

            var duplicado = await _db.Set<Cliente>()
                .AsNoTracking()
                .AnyAsync(c => c.IDCliente != id && c.Nome != null && c.Nome.ToUpper() == nome.ToUpper());

            if (duplicado)
                return Conflict(new { message = "Já existe outro cliente com esse nome." });

            cliente.Nome = nome;
            cliente.Tipo = req.Tipo;
            cliente.DataModificacao = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}