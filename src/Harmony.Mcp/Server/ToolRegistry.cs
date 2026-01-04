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

   // In-memory store for registered HRF scripts (id -> script body)
   public static readonly Dictionary<string, string> ScriptRegistry =
      new(StringComparer.OrdinalIgnoreCase);

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
   /// <param name="kernelHost">semantic kernel host</param>
   /// <returns>created/built tool registry instance is returned</returns>
   public static ToolRegistry BuildTools(
      JsonSerializerOptions jsonOptions, KernelHost kernelHost)
   {
      var config = kernelHost.Config as ProviderConfig;
      var registry = new ToolRegistry(jsonOptions, kernelHost);

      // Embeddings for a single string or a batch
      registry.AddTool(Tools.EmbeddingTool.GetTool(config!, kernelHost));

      // Semantic similarity over ad-hoc records (no index needed)
      registry.AddTool(Tools.SemanticSimilarityTool.GetTool(config!, kernelHost));

      // Chat completion as a tool (kept from your original)
      registry.AddTool(Tools.ChatCompletionTool.GetTool(config!, kernelHost));

      // Register and Validate HRF scripts
      registry.AddTool(Tools.HrfScriptRegistrationTool.GetTool(registry, ScriptRegistry));

      // Example: run an SK workflow (kept)
      registry.AddTool(Tools.WorkflowRunnerTool.GetTool(config!, kernelHost));


      return registry;
   }

}

