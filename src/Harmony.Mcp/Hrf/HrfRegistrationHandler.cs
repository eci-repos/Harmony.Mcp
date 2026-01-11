using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf;

public static class HrfRegistrationHandler
{

   /// <summary>
   /// Process HRF registration semantics used by tests/transports:
   /// - validateOnly: return validation info
   /// - overwrite: allow replacing existing scripts
   /// The handler mutates the provided script store when registration occurs.
   /// </summary>
   /// <param name="args">The "arguments" JSON element from the RPC call</param>
   /// <param name="scripts">In-memory script store (id -> script body)</param>
   /// <returns>Tool result object matching the shape expected by callers</returns>
   public static object ProcessRegistration(JsonElement args, IDictionary<string, string> scripts)
   {
      var idVal = args.GetProperty("id").GetString() ?? string.Empty;
      var script = args.GetProperty("script").GetString() ?? string.Empty;
      var validateOnly = args.TryGetProperty("validateOnly", out var v) && v.GetBoolean();
      var overwrite = args.TryGetProperty("overwrite", out var o) && o.GetBoolean();

      bool valid = true;
      JsonDocument? scriptDoc = null;
      int responseCount = 0;
      string? validationError = null;
      try
      {
         scriptDoc = JsonDocument.Parse(script);
         if (!scriptDoc.RootElement.TryGetProperty("responses", out var responses) ||
             responses.ValueKind != JsonValueKind.Array)
         {
            valid = false;
            validationError = "Missing top-level 'responses' array.";
         }
         else
         {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in responses.EnumerateArray())
            {
               if (!r.TryGetProperty("name", out var nm) || nm.ValueKind != JsonValueKind.String)
               {
                  valid = false;
                  validationError = "Each response must include a string 'name' property.";
                  break;
               }
               var rn = nm.GetString()!;
               if (!names.Add(rn))
               {
                  valid = false;
                  validationError = $"Duplicate response name: {rn}";
                  break;
               }
            }
            responseCount = names.Count;
         }
      }
      catch (JsonException ex)
      {
         valid = false;
         validationError = ex.Message;
      }
      finally
      {
         scriptDoc?.Dispose();
      }

      if (!valid)
      {
         return new { id = idVal, validated = false, registered = false, error = validationError };
      }
      else if (validateOnly)
      {
         return new { id = idVal, validated = true, responseCount };
      }
      else
      {
         if (scripts.ContainsKey(idVal) && !overwrite)
         {
            return new { id = idVal, validated = true, registered = false, error = "exists" };
         }
         else
         {
            scripts[idVal] = script;
            return new { id = idVal, validated = true, registered = true, responseCount };
         }
      }
   }

}
