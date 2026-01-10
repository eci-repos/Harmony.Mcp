using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Models;

/// <summary>
/// Result returned from the server after an HRF register/validate call.
/// </summary>
public sealed class HrfRegisterResult
{
   public string? id { get; set; }
   public bool? registered { get; set; }
   public bool? validated { get; set; }
   public int? responseCount { get; set; }

   public static HrfRegisterResult FromJsonElement(JsonElement el)
   {
      var r = new HrfRegisterResult();
      if (el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
         r.id = idEl.GetString();
      if (el.TryGetProperty("registered", out var regEl) && 
         regEl.ValueKind == JsonValueKind.True || regEl.ValueKind == JsonValueKind.False)
         r.registered = regEl.GetBoolean();
      if (el.TryGetProperty("validated", out var valEl) && 
         valEl.ValueKind == JsonValueKind.True || valEl.ValueKind == JsonValueKind.False)
         r.validated = valEl.GetBoolean();
      if (el.TryGetProperty("responseCount", out var cntEl) && 
         cntEl.ValueKind == JsonValueKind.Number)
         r.responseCount = cntEl.GetInt32();
      if (r.responseCount == null && el.TryGetProperty("responsesCount", out var altCnt) && 
         altCnt.ValueKind == JsonValueKind.Number)
         r.responseCount = altCnt.GetInt32();
      return r;
   }
}

