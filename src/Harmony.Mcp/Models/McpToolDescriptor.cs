using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpToolDescriptor
{
   [JsonPropertyName("name")] public string? Name { get; set; }
   [JsonPropertyName("description")] public string? Description { get; set; }
   [JsonPropertyName("inputSchema")] public JsonElement InputSchema { get; set; }
}

