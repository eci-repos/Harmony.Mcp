using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public static class McpJson
{
   public static readonly JsonSerializerOptions Options = new()
   {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
      WriteIndented = false,
      Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
   };
}
