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
   /// Delete a registered HRF script by id. Returns true if deleted, false if not found,
   /// or null on error.
   /// </summary>
   public async Task<bool?> DeleteRegisteredScriptAsync(string id)
   {
      try
      {
         var argsEl = JsonSerializer.SerializeToElement(new { id }, McpJson.Options);
         var resp = await _client.CallAsync<JsonElement>("hrf.delete", argsEl);
         if (resp.ValueKind == JsonValueKind.Object && resp.TryGetProperty("deleted", out var d) &&
            (d.ValueKind == JsonValueKind.True || d.ValueKind == JsonValueKind.False))
         {
            return d.GetBoolean();
         }
         return null;
      }
      catch
      {
         return null;
      }
   }

   /// <summary>
   /// List registered HRF script ids.
   /// </summary>
   public async Task<string[]?> ListRegisteredScriptsAsync()
   {
      try
      {
         var argsEl = JsonDocument.Parse("{}").RootElement;
         var resp = await _client.CallAsync<JsonElement>("hrf.list", argsEl);
         if (resp.ValueKind == JsonValueKind.Object && 
             resp.TryGetProperty("scripts", out var s) && s.ValueKind == JsonValueKind.Array)
         {
            var list = new List<string>();
            foreach (var el in s.EnumerateArray())
            {
               if (el.ValueKind == JsonValueKind.String)
                  list.Add(el.GetString()!);
            }
            return list.ToArray();
         }
         return null;
      }
      catch
      {
         return null;
      }
   }

   /// <summary>
   /// Register or validate an HRF script. Returns parsed result from the server.
   /// </summary>
   public async Task<HrfRegisterResult> RegisterScriptAsync(HrfRegisterArgs args)
   {
      var argsEl = JsonSerializer.SerializeToElement(args, McpJson.Options);
      // Call server tool
      HrfRegisterResult response;
      try
      {
         // If the script is invalid, the server will return an error with details in the message
         // and no JSON result. In that case we want to catch the error and return a failed result
         // with the error message.
         var resp = await _client.CallAsync<JsonElement>("hrf.register", argsEl);
         // server returns an object with result details
         response = HrfRegisterResult.FromJsonElement(resp);

      }
      catch (Exception ex)
      { 
         // return a failed result with the error message
         response = new HrfRegisterResult
         {
            id = args.id,
            registered = false,
            validated = false,
            responseCount = 0
         };
      }
      return response;
   }

   /// <summary>
   /// Retrieve a registered HRF script by id. Returns the raw script text
   /// or null if not found or an error occurs. This calls the server tool
   /// named 'hrf.get' which is expected to accept an object with an 'id'
   /// property and return an object containing a 'script' property.
   /// </summary>
   public async Task<string?> GetRegisteredScriptAsync(string id)
   {
      try
      {
         var argsEl = JsonSerializer.SerializeToElement(new { id }, McpJson.Options);
         var resp = await _client.CallAsync<JsonElement>("hrf.get", argsEl);
         if (resp.ValueKind == JsonValueKind.Object && 
             resp.TryGetProperty("script", out var s) && s.ValueKind == JsonValueKind.String)
            return s.GetString();
         return null;
      }
      catch
      {
         return null;
      }
   }

}

