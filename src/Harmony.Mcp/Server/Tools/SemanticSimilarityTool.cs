using Harmony.Mcp.Client;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server.Tools;

internal class SemanticSimilarityTool
{

   public static McpTool GetTool(ProviderConfig config, KernelHost kernelHost)
   {
      // Semantic similarity between two texts
      return
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

                 var embed = kernelHost.GetEmbeddingGenerator();

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
          );
   }

}
