using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server;

public sealed class ToolRegistry
{
   private readonly Dictionary<string, McpTool> _tools = new(StringComparer.OrdinalIgnoreCase);
   private readonly JsonSerializerOptions _json;

   private KernelHost? _skHost = null;

   public ToolRegistry(JsonSerializerOptions json, KernelHost host)
   {
      _json = json;
      _skHost = host;
   }

   public void AddTool(McpTool tool) => _tools[tool.Name] = tool;

   public IEnumerable<McpToolDescriptor> ListTools() => _tools.Values.Select(t => t.Descriptor);

   public async Task<(bool ok, object? result, string? error)> 
      TryCallAsync(string name, JsonElement args, CancellationToken ct)
   {
      if (!_tools.TryGetValue(name, out var tool))
         return (false, null, $"Unknown tool: {name}");
      try
      {
         var payload = JsonDocument.Parse(args.GetRawText());
         var outp = await tool.Handler(payload, ct);
         return (outp.Ok, outp.Data, outp.Error);
      }
      catch (Exception ex)
      {
         return (false, null, ex.Message);
      }
   }

   /// <summary>
   /// Build standard AI-centric tools using the provided SK host.
   /// </summary>
   /// <param name="jsonOptions">JSON options</param>
   /// <param name="skHost">semantic kernel host</param>
   /// <returns>created/built tool registry instance is returned</returns>
   public static ToolRegistry BuildTools(
      JsonSerializerOptions jsonOptions, KernelHost skHost)
   {
      var config = skHost.Config as ProviderConfig;
      var registry = new ToolRegistry(jsonOptions, skHost);

      // Embeddings for a single string or a batch
      registry.AddTool(
          new McpTool(
              name: McpHelper.EmbeddingsToolName,
              description:
                 "Return embeddings for one or more texts using the configured embedding model.",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""text"": { ""type"": ""string"" },
                    ""texts"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
                  },
                  ""oneOf"": [
                    { ""required"": [""text""] },
                    { ""required"": [""texts""] }
                  ]
                }"),
              handler: async (payload, ct) =>
              {
                 var embed = skHost.GetEmbeddingGenerator();

                 List<string> inputs = new();
                 var root = payload.RootElement;

                 if (root.TryGetProperty("text", out var one))
                    inputs.Add(one.GetString()!);

                 if (root.TryGetProperty("texts", out var many) &&
                     many.ValueKind == JsonValueKind.Array)
                    inputs.AddRange(many.EnumerateArray().
                       Select(e => e.GetString()!).Where(s => s is not null)!);

                 KernelIO.Log.WriteLine("Embeddings tool called... (" + inputs.ToString() + ")");

                 if (inputs.Count == 0)
                 {
                    return RequestResult.Fail("Provide either 'text' or 'texts'.");
                 }

                 if (config == null)
                 {
                    return RequestResult.Fail("Provider configuration is missing.");
                 }

                 var vectors = await TextChunker.GetEmbeddings(
                    embed, inputs, config.Model.ChatModel);

                 var data = new
                 {
                    count = vectors.Count,
                    dimensions = vectors[0].Length,
                    embeddings = vectors
                 };

                 return RequestResult.Okey(data);
              }
          )
      );

      // Semantic similarity over ad-hoc records (no index needed)
      registry.AddTool(
          new McpTool(
              name: McpHelper.SimilarityToolName,
              description:
                 "Given a prompt and array of records, compute cosine similarity and return " +
                 "top_k matches.",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""prompt"": { ""type"": ""string"" },
                    ""records"": {
                      ""type"": ""array"",
                      ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                          ""id"":   { ""type"": ""string"" },
                          ""text"": { ""type"": ""string"" },
                          ""meta"": { ""type"": ""object"" }
                        },
                        ""required"": [""id"", ""text""]
                      }
                    },
                    ""top_k"": { ""type"": ""integer"", ""default"": 5 },
                    ""includeEmbeddings"": { ""type"": ""boolean"", ""default"": false }
                  },
                  ""required"": [""prompt"", ""records""]
                }"),
              handler: async (payload, ct) =>
              {
                 KernelIO.Log.WriteLine("Semantic similarity tool called.");

                 var root = payload.RootElement;
                 var prompt = root.GetProperty("prompt").GetString()!;
                 var topK = root.TryGetProperty(
                    "top_k", out var kEl) ? Math.Max(1, kEl.GetInt32()) : 5;
                 var include = root.TryGetProperty(
                    "includeEmbeddings", out var incEl) && incEl.GetBoolean();

                 var records = root.GetProperty("records")
                       .EnumerateArray()
                       .Select(x => new
                       {
                          id = x.GetProperty("id").GetString()!,
                          text = x.GetProperty("text").GetString()!,
                          meta = x.TryGetProperty("meta", out var m) ? m : default(JsonElement?)
                       }).ToArray();

                 if (records.Length == 0)
                    return RequestResult.Fail("Provide at least one record.");

                 var embed = skHost.GetEmbeddingGenerator();

                 // Embed prompt
                 if (config == null)
                 {
                    return RequestResult.Fail("Provider configuration is missing.");
                 }

                 var promptVec = await TextChunker.GetEmbeddings(
                    embed, prompt, config.Model.ChatModel);

                 // Embed each record and score
                 var scored = new List<dynamic>(records.Length);
                 foreach (var r in records)
                 {
                    var recVec = await TextChunker.GetEmbeddings(
                       embed, r.text, config.Model.ChatModel);
                    var score = TextSimilarity.CosineSimilarity(promptVec, recVec);

                    scored.Add(new
                    {
                       id = r.id,
                       text = r.text,
                       score,
                       meta = r.meta.HasValue ? r.meta.Value : default(JsonElement?),
                       embedding = include ? recVec.ToArray() : null
                    });
                 }

                 var top = scored
                       .OrderByDescending(s => (double)s.score)
                       .Take(topK)
                       .ToArray();

                 return RequestResult.Okey(new
                 {
                    dimensions = promptVec.Length,
                    top_k = topK,
                    results = top
                 });
              }
          )
      );

      // Chat completion as a tool (kept from your original)
      registry.AddTool(
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

                 var chat = skHost.GetChatService();
                 var history = new ChatHistory();
                 if (!string.IsNullOrWhiteSpace(system)) history.AddSystemMessage(system);
                 history.AddUserMessage(prompt);

                 var result = await chat.GetChatMessageContentAsync(
                    history, new PromptExecutionSettings(), skHost.Instance, ct);

                 return RequestResult.Okey(new { text = result.Content ?? string.Empty });
              }
          )
      );

      // Example: run an SK workflow (kept)
      registry.AddTool(
          new McpTool(
              name: McpHelper.WorkflowRunToolName,
              description: "Run a named SK workflow with inputs (e.g., draft->summarize).",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""inputs"": { ""type"": ""object"" }
                  },
                  ""required"": [""name""]
                }"),
              handler: async (payload, ct) => await skHost.RunWorkflowAsync(payload, ct)
          )
      );

      return registry;
   }

}

