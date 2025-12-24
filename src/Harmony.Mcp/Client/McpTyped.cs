using Harmony.Mcp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Client;

/// <summary>
/// Provides utility methods for invoking typed calls to an MCP (Modular Command Processor) tool 
/// and validating input data against a JSON schema.
/// </summary>
/// <remarks>This class is designed to facilitate interaction with MCP tools by enabling 
/// strongly-typed input and output handling. It also includes functionality for validating input 
/// data against a JSON schema to ensure compliance with expected formats.</remarks>
public static class McpTyped
{

   public static async Task<TRes> CallAsync<TArg, TRes>(
      McpClient client, string toolName, TArg args, McpToolDescriptor? desc = null)
   {
      var el = JsonSerializer.SerializeToElement(args, McpJson.Options);
      if (desc != null)
         ValidateAgainstSchema(el, desc.InputSchema);
      return await client.CallAsync<TRes>(toolName, el);
   }

   public static void ValidateAgainstSchema(JsonElement value, JsonElement schema)
   {
      if (schema.ValueKind != JsonValueKind.Object) return;
      if (schema.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "object")
      {
         if (schema.TryGetProperty("required", out var reqEl) && 
            reqEl.ValueKind == JsonValueKind.Array)
         {
            foreach (var r in reqEl.EnumerateArray())
            {
               var name = r.GetString();
               if (!String.IsNullOrWhiteSpace(name) && !value.TryGetProperty(name, out _))
                  throw new ArgumentException($"Missing required property: {name}");
            }
         }
         if (schema.TryGetProperty("properties", out var props) && 
            props.ValueKind == JsonValueKind.Object)
         {
            foreach (var prop in props.EnumerateObject())
            {
               if (value.TryGetProperty(prop.Name, out var v))
               {
                  if (prop.Value.TryGetProperty("type", out JsonElement t))
                  {
                     var typeString = t.GetString();
                     if (typeString != null && !TypeMatches(v, typeString))
                        throw new ArgumentException($"Property '{prop.Name}' expected type " +
                           $"{typeString}");
                  }
               }
            }
         }
      }
   }

   private static bool TypeMatches(JsonElement v, string t) => t switch
   {
      "string" => v.ValueKind == JsonValueKind.String,
      "number" => v.ValueKind is JsonValueKind.Number,
      "integer" => v.ValueKind is JsonValueKind.Number,
      "object" => v.ValueKind is JsonValueKind.Object,
      "array" => v.ValueKind is JsonValueKind.Array,
      "boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
      _ => true
   };

}
