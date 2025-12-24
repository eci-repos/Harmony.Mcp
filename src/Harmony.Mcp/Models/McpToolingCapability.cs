using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpToolingCapability
{
   [JsonPropertyName("listChanged")] public bool ListChanged { get; set; }
}

