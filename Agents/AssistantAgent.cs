using FitpriseVA.Tools;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FitpriseVA.Agents;

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
}


public class AssistantAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;


    public AssistantAgent(Kernel kernel)
    {
        _kernel = kernel;
        _chat = kernel.GetRequiredService<IChatCompletionService>();
    }


    private const string SystemPrompt =
        @"You are an autonomous assistant embedded in an ASP.NET application.
        TOOLS: You may invoke functions; default to `web_search` when external/public information is needed.
        DATABASE: Conversation history is persisted by the host app; request summaries when context is required.
        STYLE: Respond concisely, focus on actionable outputs, and avoid unnecessary elaboration.";


    public async Task<string> GetReplyAsync(IEnumerable<(string role, string content)> history, CancellationToken ct)
    {
        var chat = new ChatHistory(SystemPrompt);
        foreach (var (role, content) in history)
        {
            chat.AddMessage(role.Equals("user", StringComparison.OrdinalIgnoreCase) ? AuthorRole.User : AuthorRole.Assistant, content);
        }


        var stream = _chat.GetStreamingChatMessageContentsAsync(
        chat,
        new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.3
        },
        _kernel,
        ct);

        var final = new System.Text.StringBuilder();
        await foreach (var item in stream.WithCancellation(ct))
        {
            if (item.Content is not null) final.Append(item.Content);
        }
        return final.ToString();
    }
}