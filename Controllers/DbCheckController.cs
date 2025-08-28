using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FitpriseVA.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DbCheckController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public DbCheckController(IConfiguration cfg) => _cfg = cfg;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var connStr = _cfg.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connStr))
            return StatusCode(500, new { ok = false, error = "Missing ConnectionStrings:DefaultConnection" });

        try
        {
            using var conn = new SqlConnection(connStr);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(6)); // hard cap

            await conn.OpenAsync(cts.Token);
            using var cmd = new SqlCommand("SELECT 1", conn) { CommandTimeout = 5 };
            var result = await cmd.ExecuteScalarAsync(cts.Token);

            return Ok(new { ok = true, result });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { ok = false, error = "SQL connect timed out (≤6s). Check server/instance/firewall." });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new { ok = false, sql = ex.Number, error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { ok = false, error = ex.Message });
        }
    }
}
