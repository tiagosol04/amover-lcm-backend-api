using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API_AMOVER.Controllers
{
    [ApiController]
    [Route("api/me")]
    public class MeController : ControllerBase
    {
        [Authorize]
        [HttpGet]
        public IActionResult Get()
        {
            string? userId =
                User.FindFirstValue("uid") ??
                User.FindFirstValue("sub") ??
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            string? username =
                User.FindFirstValue("unique_name") ??
                User.FindFirstValue(ClaimTypes.Name) ??
                User.Identity?.Name;

            string? email =
                User.FindFirstValue("email") ??
                User.FindFirstValue(ClaimTypes.Email);

            var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).Distinct().ToArray();

            return Ok(new { userId, username, email, roles });
        }
    }
}
