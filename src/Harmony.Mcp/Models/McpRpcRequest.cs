using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpRpcRequest
{
   [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
   [JsonPropertyName("id")] public string? Id { get; set; }
   [JsonPropertyName("method")] public string? Method { get; set; }
   [JsonPropertyName("params")] public JsonElement? Params { get; set; }
}
