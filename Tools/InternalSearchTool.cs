using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace FitpriseVA.Tools
{
    public class InternalSearchOptions
    {
        /// <summary>Optional path to a schema prompt file (markdown/text/JSON). If null, tool will derive schema from INFORMATION_SCHEMA.</summary>
        public string? SchemaPath { get; set; }
        /// <summary>Max rows to return per query to keep results snappy.</summary>
        public int MaxRows { get; set; } = 200;
    }

    public class InternalSearchTool
    {
        private readonly string _connString;
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;
        private readonly InternalSearchOptions _opts;

        public InternalSearchTool(
            Kernel kernel,
            IOptions<InternalSearchOptions> opts,
            IConfiguration cfg)
        {
            _kernel = kernel;
            _opts = opts.Value;
            _connString = cfg.GetConnectionString("DefaultConnection")
                          ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

            // Resolve the chat service from the Kernel (so we don't require it via DI at construction time)
            _chat = _kernel.GetRequiredService<IChatCompletionService>();
        }

        [KernelFunction("internal_search")]
        [Description("Convert a natural-language question into a SAFE, SELECT-ONLY SQL query using the known schema, execute it, and return a compact table.")]
        
        public async Task<string> InternalSearchAsync(
            [Description("Natural-language question about internal data (NOT the SQL).")] string question,
            CancellationToken ct = default)
        {
            try
            {
                // 1) Build schema context
                var schemaText = await GetSchemaTextAsync(ct);

                // 2) Ask the model to produce SAFE SELECT-only SQL (max 6 columns, joins ok)
                var sys =
                    "You generate strictly SAFE, SELECT-ONLY T-SQL for SQL Server.\n" +
                    "Rules:\n" +
                    " - SELECT statements only. No INSERT/UPDATE/DELETE/MERGE/EXEC/DDL.\n" +
                    " - Limit to the 6 most informative columns.\n" +
                    " - Use explicit JOINs when needed.\n" +
                    " - Use TOP if the result could be large (default TOP 200).\n" +
                    " - Avoid fuzzy LIKE patterns unless asked; prefer exact or parameterizable filters.\n" +
                    " - Never use xp_cmdshell or dangerous functions.\n" +
                    " - Always end with a semicolon.\n" +
                    "Return ONLY the SQL between <sql> and </sql> tags.";

                var chat = new ChatHistory(sys);
                chat.AddSystemMessage("Database schema:\n" + schemaText);
                chat.AddUserMessage("Question: " + question);

                var sqlResp = await _chat.GetChatMessageContentAsync(
                chat,
                new OpenAIPromptExecutionSettings { Temperature = 0.0 },
                _kernel,
                ct);


                var raw = sqlResp.Content ?? string.Empty;
                var sql = ExtractTag(raw, "sql").Trim();

                // 3) Validate safety
                if (!IsSafeSelect(sql)) return "Refused: generated SQL was not safe SELECT-only.";

                // 4) Enforce TOP cap if missing
                sql = EnsureTopLimit(sql, _opts.MaxRows);

                // 5) Execute and render
                var table = await QueryAsync(sql, ct);
                return RenderMarkdown(sql, table);
            }
            catch (TimeoutException tex)
            {
                return $"_DB timeout: {tex.Message}_";
            }
            catch (SqlException sqlex)
            {
                return $"_DB error: {sqlex.Number} — {sqlex.Message}_";
            }
            catch (Exception ex)
            {
                return $"_Internal search failed: {ex.Message}_";
            }
        }

        private static string ExtractTag(string text, string tag)
        {
            var start = $"<{tag}>";
            var end = $"</{tag}>";
            var i = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            var j = text.IndexOf(end, StringComparison.OrdinalIgnoreCase);
            if (i < 0 || j < 0 || j <= i) return text;
            return text[(i + start.Length)..j];
        }

        private static bool IsSafeSelect(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;
            var s = sql.Trim().ToUpperInvariant();
            if (!s.StartsWith("SELECT")) return false;
            // Quick-and-conservative keyword denylist
            string[] bad = ["INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE", "MERGE", "EXEC", "EXECUTE", "GRANT", "REVOKE", "DENY", "BACKUP", "RESTORE"];
            return bad.All(k => !s.Contains(k));
        }

        private static string EnsureTopLimit(string sql, int maxRows)
        {
            // naive TOP injector for SELECT without TOP
            var s = sql.Trim();
            var upper = s.ToUpperInvariant();
            if (upper.StartsWith("SELECT") && !upper.StartsWith("SELECT TOP"))
            {
                return "SELECT TOP " + maxRows + s[6..];
            }
            return s;
        }

        private async Task<DataTable> QueryAsync(string sql, CancellationToken ct)
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                // Opening can hang on bad DNS/firewall; give it its own timeout window
                using var openCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                openCts.CancelAfter(TimeSpan.FromSeconds(20));
                await conn.OpenAsync(openCts.Token);

                using var cmd = new SqlCommand(sql, conn)
                {
                    CommandType = CommandType.Text,
                    CommandTimeout = 60
                };
                using var reader = await cmd.ExecuteReaderAsync(ct);
                var dt = new DataTable();
                dt.Load(reader);
                return dt;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("SQL connection or query timed out.");
            }
        }


        private static string RenderMarkdown(string sql, DataTable dt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("**SQL**");
            sb.AppendLine("```sql");
            sb.AppendLine(sql);
            sb.AppendLine("```");

            if (dt.Rows.Count == 0)
            {
                sb.AppendLine("_No rows returned._");
                return sb.ToString();
            }

            // header
            sb.Append("| ");
            foreach (DataColumn c in dt.Columns) sb.Append(c.ColumnName).Append(" | ");
            sb.AppendLine();
            sb.Append("| ");
            foreach (DataColumn _ in dt.Columns) sb.Append("--- | ");
            sb.AppendLine();

            // rows (cap to first 50 for readability)
            var count = Math.Min(dt.Rows.Count, 50);
            for (int r = 0; r < count; r++)
            {
                sb.Append("| ");
                foreach (DataColumn c in dt.Columns)
                {
                    var val = dt.Rows[r][c] is DBNull ? "" : dt.Rows[r][c]?.ToString();
                    sb.Append(val).Append(" | ");
                }
                sb.AppendLine();
            }
            if (dt.Rows.Count > count) sb.AppendLine($"_… {dt.Rows.Count - count} more rows omitted._");
            return sb.ToString();
        }

        private async Task<string> GetSchemaTextAsync(CancellationToken ct)
        {
            // Option A: read pre-configured schema file
            if (!string.IsNullOrWhiteSpace(_opts.SchemaPath) && File.Exists(_opts.SchemaPath))
            {
                return await File.ReadAllTextAsync(_opts.SchemaPath!, ct);
            }

            // Option B: derive from INFORMATION_SCHEMA (top N tables/columns)
            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync(ct);

            var sql = @"
                SELECT TOP 50
                    TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;";

            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync(ct);

            var byTable = new Dictionary<string, List<(string col, string typ)>>();
            while (await rdr.ReadAsync(ct))
            {
                var schema = rdr.GetString(0);
                var table = rdr.GetString(1);
                var col = rdr.GetString(2);
                var typ = rdr.GetString(3);
                var key = $"[{schema}].[{table}]";
                if (!byTable.TryGetValue(key, out var list))
                {
                    list = new List<(string, string)>();
                    byTable[key] = list;
                }
                list.Add((col, typ));
            }

            var sb = new StringBuilder("# Database Schema (partial)\n");
            foreach (var kv in byTable)
            {
                sb.AppendLine($"## {kv.Key}");
                foreach (var (col, typ) in kv.Value)
                    sb.AppendLine($"- {col} ({typ})");
            }
            return sb.ToString();
        }
    }
}
