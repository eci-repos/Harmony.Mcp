using Harmony.Mcp.Client;
using Harmony.Mcp.Hrf.Models;
using Harmony.Mcp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf;

/// <summary>
/// Client helpers for submitting HRF-related requests (hrf.register and per-response tool calls).
/// </summary>
public sealed class HrfClient
{
   private readonly McpClient _client;

   public HrfClient(McpClient client)
   {
      _client = client;
   }

   /// <summary>
   /// Register or validate an HRF script. Returns parsed result from the server.
   /// </summary>
   public async Task<HrfRegisterResult> RegisterScriptAsync(HrfRegisterArgs args)
   {
      var argsEl = JsonSerializer.SerializeToElement(args, McpJson.Options);
      // Call server tool
      var resp = await _client.CallAsync<JsonElement>("hrf.register", argsEl);
      // server returns an object with result details
      return HrfRegisterResult.FromJsonElement(resp);
   }

   /// <summary>
   /// Call a single registered HRF response tool by id and response name.
   /// If the tool expects no inputs, pass an empty object.
   /// </summary>
   public async Task<HrfResponseResult> CallResponseAsync(
      string id, string responseName, object? inputs = null)
   {
      var toolName = $"{id}.{responseName}";
      JsonElement argsEl;
      if (inputs == null)
         argsEl = JsonDocument.Parse("{}").RootElement;
      else
         argsEl = JsonSerializer.SerializeToElement(inputs, McpJson.Options);

      var resp = await _client.CallAsync<JsonElement>(toolName, argsEl);
      return HrfResponseResult.FromJsonElement(resp);
   }

}

