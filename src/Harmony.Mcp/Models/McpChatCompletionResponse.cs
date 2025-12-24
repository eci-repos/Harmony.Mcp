using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harmony.Mcp.Models;

public record McpChatCompletionResponse
{
   public string? text { get; init; }
}
