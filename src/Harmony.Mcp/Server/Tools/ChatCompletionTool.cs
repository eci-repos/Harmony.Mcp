using Harmony.Mcp.Client;
using Harmony.SemanticKernel.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server.Tools;

internal class ChatCompletionTool
{

   public static McpTool GetTool(ProviderConfig config, KernelHost kernelHost)
   {
      // Chat completion tool
      return
          new McpTool(
              name: McpHelper.ChatCompletionToolName,
              description: "Call the configured chat model with a prompt.",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""prompt"": { ""type"": ""string"" },
                    ""system"": { ""type"": ""string"" },
                    ""max_tokens"": { ""type"": ""integer"" }
                  },
                  ""required"": [""prompt""]
                }"),
              handler: async (payload, ct) =>
              {
                 KernelIO.Log.WriteLine("Chat completion tool called.");

                 var prompt = payload.RootElement.GetProperty("prompt").GetString()!;
                 var system = payload.RootElement.
                    TryGetProperty("system", out var sysEl) ? sysEl.GetString() : null;
                 var maxToks = payload.RootElement.
                    TryGetProperty("max_tokens", out var mtEl) ? mtEl.GetInt32() : (int?)null;

                 var chat = kernelHost.GetChatService();
                 var history = new ChatHistory();
                 if (!string.IsNullOrWhiteSpace(system)) history.AddSystemMessage(system);
                 history.AddUserMessage(prompt);

                 var result = await chat.GetChatMessageContentAsync(
                    history, new PromptExecutionSettings(), kernelHost.Instance, ct);

                 return RequestResult.Okey(new { text = result.Content ?? string.Empty });
              }
          );
   }
}
