using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public class McpSimilarityArgs
{
   public string? prompt { get; init; }
   public McpSimilarityRecord[]? records { get; init; }
   public int? top_k { get; init; }
   public bool? includeEmbeddings { get; init; }
}
