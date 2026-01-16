using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMOVER.Controllers
{
    [ApiController]
    [Route("api/me")]
    public class MeController : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public ActionResult<object> GetMe()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var username = User.FindFirstValue(ClaimTypes.Name) ?? "";
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
            var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value).Distinct().ToList();

            return Ok(new
            {
                userId,
                username,
                email,
                roles
            });
        }
    }
}
