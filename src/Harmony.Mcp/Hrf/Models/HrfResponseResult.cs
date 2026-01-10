using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Models;

/// <summary>
/// Result returned when calling a per-response HRF tool.
/// </summary>
public sealed class HrfResponseResult
{
   public string? id { get; set; }
   public string? name { get; set; }
   /// <summary>
   /// Content will be either a string or a JsonElement for structured content.
   /// If structured, the raw JsonElement is stored in RawContent; StringContent is non-null for 
   /// primitive string content.
   /// </summary>
   public string? StringContent { get; set; }
   public JsonElement? RawContent { get; set; }

   public static HrfResponseResult FromJsonElement(JsonElement el)
   {
      var r = new HrfResponseResult();
      if (el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
         r.id = idEl.GetString();
      if (el.TryGetProperty("name", out var nmEl) && nmEl.ValueKind == JsonValueKind.String)
         r.name = nmEl.GetString();
      if (el.TryGetProperty("content", out var contentEl))
      {
         if (contentEl.ValueKind == JsonValueKind.String)
            r.StringContent = contentEl.GetString();
         else
            r.RawContent = contentEl;
      }
      return r;
   }
}

