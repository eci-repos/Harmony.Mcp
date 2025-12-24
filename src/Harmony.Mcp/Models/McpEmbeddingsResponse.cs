using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public record McpEmbeddingsResponse
{
   public int count { get; init; }
   public int dimensions { get; init; }
   public double[][]? embeddings { get; init; }
}

