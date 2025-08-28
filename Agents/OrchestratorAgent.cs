using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FitpriseVA.Agents
{
    public class OrchestratorAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;

        private const string SystemPrompt =
           @"ROLE: You are the Orchestrator. Decide what the user needs and call tools. 
             DECISIONS: 
              - If the prompt requires GENERAL INTERNET info (news/docs/how-tos), call tool: google.web_search(query). 
              - If the prompt requires INTERNAL DB info (facts from SQL Server), call tool: internal.internal_search(question). 
              - Some prompts need BOTH. Split the prompt and call both tools; then merge results succinctly. 
             CONSTRAINTS: 
              - Never fabricate internal data. Use internal_search for anything DB-backed. 
              - When summarizing web results, include brief links (one per source max). 
              - Be concise and actionable.";

        public OrchestratorAgent(Kernel kernel)
        {
            _kernel = kernel;
            _chat = kernel.GetRequiredService<IChatCompletionService>();
        }

        public async Task<string> GetReplyAsync(string userInput, CancellationToken ct)
        {
            Debug.WriteLine("In GetReplyAsync");

            var chat = new ChatHistory(SystemPrompt);
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
                return string.IsNullOrWhiteSpace(text) ? "_No response generated._" : text;
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
    }
}
