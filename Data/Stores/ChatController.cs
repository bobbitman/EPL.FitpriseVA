using FitpriseVA.Agents;
using FitpriseVA.Data.Stores;
using Microsoft.AspNetCore.Mvc;

namespace FitpriseVA.Data.Stores
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController(AssistantAgent agent, ConversationStore store) : ControllerBase
    {
        public record ChatRequest(Guid? ConversationId, string Input);
        public record ChatResponse(Guid ConversationId, string Output);


        [HttpPost]
        public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest req, CancellationToken ct)
        {
            var conv = await store.GetOrCreateAsync(req.ConversationId, ct);
            await store.AddMessageAsync(conv.Id, "user", req.Input, ct);


            var history = conv.Messages
            .OrderBy(m => m.CreatedUtc)
            .Select(m => (m.Role, m.Content));


            var output = await agent.GetReplyAsync(history, ct);
            await store.AddMessageAsync(conv.Id, "assistant", output, ct);


            return Ok(new ChatResponse(conv.Id, output));
        }
    }
}
