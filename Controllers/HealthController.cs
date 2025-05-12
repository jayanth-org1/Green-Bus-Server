using Microsoft.AspNetCore.Mvc;

namespace TransportBooking.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class healthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
    
} 