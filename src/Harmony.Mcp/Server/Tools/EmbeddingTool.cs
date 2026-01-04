using Harmony.Mcp.Client;
using Harmony.SemanticKernel.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------

namespace Harmony.Mcp.Server.Tools;

internal class EmbeddingTool
{

   public static McpTool GetTool(ProviderConfig config, KernelHost kernelHost)
   {

      // Embeddings for a single string or a batch
      return
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
                 var embed = kernelHost.GetEmbeddingGenerator();

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
      );
   }

}
