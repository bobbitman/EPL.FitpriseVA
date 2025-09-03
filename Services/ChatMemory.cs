using Microsoft.Extensions.Caching.Memory;

namespace FitpriseVA.Services
{
    public interface IChatMemory
    {
        IReadOnlyList<(string role, string content)> Get(string conversationId);
        void Append(string conversationId, string role, string content, int maxTurns = 16);
    }

    public sealed class ChatMemory : IChatMemory
    {
        private readonly IMemoryCache _cache;
        public ChatMemory(IMemoryCache cache) => _cache = cache;

        public IReadOnlyList<(string role, string content)> Get(string conversationId) =>
            _cache.TryGetValue(conversationId, out List<(string role, string content)> list)
                ? list : new();

        public void Append(string conversationId, string role, string content, int maxTurns = 16)
        {
            var list = _cache.GetOrCreate(conversationId, _ => new List<(string, string)>());
            list.Add((role, content));
            // keep last N turns (user+assistant = 2 per turn)
            if (list.Count > maxTurns * 2) list.RemoveRange(0, list.Count - maxTurns * 2);
        }
    }
}
