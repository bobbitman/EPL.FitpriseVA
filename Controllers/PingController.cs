using Microsoft.AspNetCore.Mvc;

namespace FitpriseVA.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    [HttpPost]
    //public IActionResult Post() => Ok(new { ok = true, msg = "pong" });

    public IActionResult Post()
    {
        Console.WriteLine("HIT /api/ping");
        return Ok(new { ok = true, msg = "pong" });
    }
}