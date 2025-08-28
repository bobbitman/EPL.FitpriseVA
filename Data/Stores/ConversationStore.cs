using FitpriseVA.Data;
using FitpriseVA.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitpriseVA.Data.Stores
{
    public class ConversationStore(AppDbContext db)
    {
        public async Task<Conversation> GetOrCreateAsync(Guid? id, CancellationToken ct)
        {
            if (id.HasValue)
            {
                var existing = await db.Conversations
                .Include(c => c.Messages.OrderBy(m => m.CreatedUtc))
                .FirstOrDefaultAsync(c => c.Id == id.Value, ct);
                if (existing != null) return existing;
            }
            var conv = new Conversation();
            db.Conversations.Add(conv);
            await db.SaveChangesAsync(ct);
            return conv;
        }


        public async Task AddMessageAsync(Guid conversationId, string role, string content, CancellationToken ct)
        {
            db.Messages.Add(new Message
            {
                ConversationId = conversationId,
                Role = role,
                Content = content
            });
            var conv = await db.Conversations.FirstAsync(c => c.Id == conversationId, ct);
            conv.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
