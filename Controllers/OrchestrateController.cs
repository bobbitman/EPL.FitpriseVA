using FitpriseVA.Agents;
using FitpriseVA.Data.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Diagnostics;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.EntityFrameworkCore;
using FitpriseVA.Data;


namespace FitpriseVA.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrchestrateController : ControllerBase
{
    public record OrchestrateRequest(Guid? ConversationId, string Input);

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OrchestrateRequest req, CancellationToken ct)
    {
        var store = HttpContext.RequestServices.GetRequiredService<ConversationStore>();
        var agent = HttpContext.RequestServices.GetRequiredService<OrchestratorAgent>();

        try
        {
            Debug.WriteLine("DB: GetOrCreateAsync…");
            var conv = await store.GetOrCreateAsync(req.ConversationId, ct);

            Debug.WriteLine("DB: AddMessageAsync(user)...");
            await store.AddMessageAsync(conv.Id, "user", req.Input, ct);

            Debug.WriteLine("Agent: calling GetReplyAsync…");
            var conversationId = (req.ConversationId == Guid.Empty ? conv.Id : req.ConversationId).ToString();
            var output = await agent.GetReplyAsync(req.Input, ct, conversationId);

            Debug.WriteLine("DB: AddMessageAsync(assistant)...");
            await store.AddMessageAsync(conv.Id, "assistant", output, ct);

            return Ok(new { ok = true, conversationId = conv.Id, output });
        }
        catch (SqlException ex)
        {
            Debug.WriteLine($"DB ERROR SQL#{ex.Number}: {ex.Message}");
            return StatusCode(500, new { error = $"SQL#{ex.Number}: {ex.Message}" });
        }
    }
}