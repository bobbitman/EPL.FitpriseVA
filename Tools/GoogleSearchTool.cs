using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FitpriseVA.Tools
{
    public class GoogleSearchOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Cx { get; set; } = string.Empty; // Programmable Search Engine ID
    }

    public record GoogleSearchResultItem(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("link")] string Link,
        [property: JsonPropertyName("snippet")] string Snippet
    );

    // SINGLE constructor so DI knows which one to use
    public class GoogleSearchTool
    {
        private readonly HttpClient _http;
        private readonly GoogleSearchOptions _opts;

        public GoogleSearchTool(HttpClient http, IOptions<GoogleSearchOptions> opts)
        {
            _http = http;
            _opts = opts.Value;
            _http.Timeout = TimeSpan.FromSeconds(15); // fail fast in dev
        }

        [KernelFunction("web_search")]
        [Description("Search the public internet via Google Programmable Search. Use for news, general knowledge, docs, how-tos.")]
        public async Task<string> SearchAsync(
            [Description("User query to search for")] string query,
            [Description("Max items to return (default 5)")] int maxResults = 5,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opts.ApiKey) || string.IsNullOrWhiteSpace(_opts.Cx))
                return "_Google search is not configured. Set Google:ApiKey and Google:Cx_";

            var url = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(query)}&key={_opts.ApiKey}&cx={_opts.Cx}";
            try
            {
                using var resp = await _http.GetAsync(url, ct);
                var raw = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    return $"_Google error {((int)resp.StatusCode)}: {raw}_";

                var doc = JsonSerializer.Deserialize<JsonElement>(raw);
                if (!doc.TryGetProperty("items", out var items)) return "No results.";

                var take = Math.Min(maxResults, items.GetArrayLength());
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < take; i++)
                {
                    var it = items[i];
                    var title = it.GetProperty("title").GetString();
                    var link = it.GetProperty("link").GetString();
                    var snippet = it.TryGetProperty("snippet", out var sn) ? sn.GetString() : "";
                    sb.AppendLine($"- {title}\n  {link}\n  {snippet}");
                }
                return sb.ToString();
            }
            catch (TaskCanceledException)
            {
                return "_Google search timed out (15s). Try again or refine the query._";
            }
            catch (Exception ex)
            {
                return $"_Google search failed: {ex.Message}_";
            }
        }
    }
}
