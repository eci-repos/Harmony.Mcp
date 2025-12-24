using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public class McpSimilarityResult
{
   public string? id { get; set; }
   public string? text { get; set; }
   public double score { get; init; }
   public JsonElement? meta { get; set; }
   public double[]? embedding { get; set; }
}
