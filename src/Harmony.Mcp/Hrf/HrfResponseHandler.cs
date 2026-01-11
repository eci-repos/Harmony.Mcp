using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf;

public static class HrfResponseHandler
{

   /// <summary>
   /// Build the per-response tool result for a given script id and response name.
   /// Returns (ok, toolResult, error). toolResult is suitable for direct JSON serialization
   /// as the MCP tool result (shape: { id, name, content }).
   /// </summary>
   public static (bool ok, object? toolResult, string? error) BuildPerResponseResult(
      string scriptId, string responseName, IDictionary<string, string> scripts)
   {
      if (!scripts.TryGetValue(scriptId, out var scriptText))
         return (false, null, $"Unknown script id: {scriptId}");

      try
      {
         using var sd = JsonDocument.Parse(scriptText);
         var rootEl = sd.RootElement;
         if (!rootEl.TryGetProperty("responses", out var responses) ||
             responses.ValueKind != JsonValueKind.Array)
         {
            return (false, null, "Script missing 'responses' array.");
         }

         foreach (var r in responses.EnumerateArray())
         {
            if (r.TryGetProperty("name", out var nm) &&
                nm.ValueKind == JsonValueKind.String &&
                string.Equals(nm.GetString(), responseName, StringComparison.OrdinalIgnoreCase))
            {
               if (!r.TryGetProperty("content", out var contentEl))
                  return (false, null, "Response has no 'content'.");

               object contentData;
               if (contentEl.ValueKind == JsonValueKind.String)
               {
                  // extract string value
                  contentData = contentEl.GetString()!;
               }
               else
               {
                  // produce a JsonElement that is independent for serialization
                  contentData = JsonDocument.Parse(contentEl.GetRawText()).RootElement;
               }

               var toolResult = new
               {
                  id = scriptId,
                  name = responseName,
                  content = contentData
               };

               return (true, toolResult, null);
            }
         }

         return (false, null, $"Response '{responseName}' not found.");
      }
      catch (JsonException ex)
      {
         return (false, null, ex.Message);
      }
   }

}
