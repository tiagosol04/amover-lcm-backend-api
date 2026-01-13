using API_AMOVER.Data;
using API_AMOVER.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_Amover.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly LcmContext _db;

        public HealthController(LcmContext db) => _db = db;

        [HttpGet]
        public IActionResult Get() =>
            Ok(new { status = "ok", utc = DateTime.UtcNow });

        [HttpGet("db")]
        public async Task<IActionResult> Db()
        {
            var clientes = await _db.Set<Cliente>().AsNoTracking().CountAsync();
            var encomendas = await _db.Set<Encomenda>().AsNoTracking().CountAsync();
            var ordens = await _db.Set<OrdemProducao>().AsNoTracking().CountAsync();
            var motas = await _db.Set<Mota>().AsNoTracking().CountAsync();

            return Ok(new
            {
                status = "db_ok",
                counts = new { clientes, encomendas, ordens, motas }
            });
        }
    }
}
