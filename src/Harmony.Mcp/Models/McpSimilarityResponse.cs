using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public class McpSimilarityResponse
{
   public int dimensions { get; init; }
   public int top_k { get; init; }
   public McpSimilarityResult[]? results { get; init; }
}
