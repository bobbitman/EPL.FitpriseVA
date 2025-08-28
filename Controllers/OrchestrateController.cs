using FitpriseVA.Agents;
using FitpriseVA.Data.Stores;
using Microsoft.AspNetCore.Mvc;
using System;

namespace FitpriseVA.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrchestrateController : ControllerBase
{
    [HttpPost]
    public IActionResult Post()
    {
        Console.WriteLine("HIT /api/orchestrate (start)");
        return Ok(new { ok = true });  // return immediately for this test
    }
}