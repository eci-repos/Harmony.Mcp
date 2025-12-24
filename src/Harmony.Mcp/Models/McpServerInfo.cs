using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpServerInfo
{
   [JsonPropertyName("name")] public string? Name { get; set; }
   [JsonPropertyName("version")] public string? Version { get; set; }
}
