using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpInitializeResult
{
   [JsonPropertyName("protocolVersion")] public string? ProtocolVersion { get; set; }
   [JsonPropertyName("serverInfo")] public McpServerInfo? ServerInfo { get; set; }
   [JsonPropertyName("capabilities")] public McpCapabilities? Capabilities { get; set; }
}

