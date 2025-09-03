using FitpriseVA.Services;
using FitpriseVA.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Data;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FitpriseVA.Agents
{
    public class OrchestratorAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;
        private readonly IServiceProvider _sp;
        private readonly IChatMemory _mem;
        private bool _internalAdded;


        private const string SystemPrompt =
           @"ROLE: You are the Orchestrator. Decide what the user needs and call tools.
             DECISIONS:
              - If the prompt requires GENERAL INTERNET info (news/docs/how-tos), call tool: google.web_search(query).
              - If the prompt requires INTERNAL DB info (facts from SQL Server), call tool: internal.internal_search(question).
              - Some prompts need BOTH. Split the prompt and call both tools; then merge results succinctly.
             COREFERENCE:
              - Treat pronouns like “these”, “them”, “that”, “it”, and follow-ups like “list them”, “show details”, “shorten to 5” as referring to the last INTERNAL entity you discussed.
              - For such follow-ups, PREFER internal.internal_search unless the user explicitly asks for “web/internet/Google/news”.
             CONSTRAINTS:
              - Never fabricate internal data. Use internal_search for anything DB-backed.
              - When summarizing web results, include brief links (one per source max).
              - Be concise and actionable.";

        public OrchestratorAgent(Kernel kernel, IChatCompletionService chat, IServiceProvider sp, IChatMemory mem)
        {
            _kernel = kernel;
            _chat = chat;
            _sp = sp;
            _mem = mem;
        }


        public async Task<string> GetReplyAsync(string userInput, CancellationToken ct, string? conversationId = null)
        {
            Debug.WriteLine("In GetReplyAsync");
            EnsureInternalToolAdded();

            var convId = conversationId ?? "default";
            _mem.Append(convId, "user", userInput);

            var chat = new ChatHistory(SystemPrompt);

            foreach (var (role, content) in _mem.Get(convId))
            {
                if (role == "user") chat.AddUserMessage(content);
                else chat.AddAssistantMessage(content);
            }


            chat.AddUserMessage(userInput);

            // Hard timeout so the UI never hangs forever.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            try
            {
                var stream = _chat.GetStreamingChatMessageContentsAsync(
                    chat,
                    new OpenAIPromptExecutionSettings
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                        Temperature = 0.2
                    },
                    _kernel,
                    cts.Token);

                var final = new System.Text.StringBuilder();
                await foreach (var item in stream.WithCancellation(cts.Token))
                {
                    if (item.Content is not null) final.Append(item.Content);
                }
                var text = final.ToString().Trim();
                if (string.IsNullOrWhiteSpace(text)) return "_No response generated._";
                _mem.Append(convId, "assistant", text);
                return text;
            }
            catch (OperationCanceledException)
            {
                return "_Timed out after 60s. Try a narrower question or check API keys/network._";
            }
            catch (Exception ex)
            {
                // Bubble a concise error back to the UI
                return $"_Error: {ex.GetType().Name} — {ex.Message}_";
            }
        }

        private void EnsureInternalToolAdded()
        {
            if (_internalAdded) return;

            // already present on the kernel (e.g., from a previous request)
            if (_kernel.Plugins.TryGetPlugin("internal", out _))
            {
                _internalAdded = true;
                return;
            }

            // add once
            var internalTool = _sp.GetRequiredService<InternalSearchTool>();
            _kernel.Plugins.AddFromObject(internalTool, "internal");
            _internalAdded = true;
        }

    }
}
