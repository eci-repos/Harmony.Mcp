using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harmony.Mcp.Hrf.Protocols;

// -------------------------------------------------------------------------------------------------
internal class HrfMediaType
{
   public const string HRF_MEDIA_TYPE = "application/harmony+json";
   public const string HRF_SCRIPT_MEDIA_TYPE = "application/harmony-script+json";

   public static readonly HashSet<string> HrfMediaTypes = new(StringComparer.OrdinalIgnoreCase)
   {
      HRF_MEDIA_TYPE,
      HRF_SCRIPT_MEDIA_TYPE
   };

   public static string NormalizeMediaType(string contentType)
   {
      if (string.IsNullOrWhiteSpace(contentType))
         return string.Empty;
      var idx = contentType.IndexOf(';');
      return (idx >= 0 ? contentType.Substring(0, idx) : contentType).Trim();
   }

   public static bool IsHrfContentMediaType(string contentType)
   {
      var media = NormalizeMediaType(contentType);
      return HrfMediaTypes.Contains(media);
   }

   public static bool IsHrfMediaType(string mediaType)
   {
      if (string.IsNullOrEmpty(mediaType)) return false;
      var types = HrfMediaType.HrfMediaTypes.ToArray<string>();
      for (var i = 0; i < types.Length; i++)
      {
         if (string.Equals(mediaType, types[i], StringComparison.OrdinalIgnoreCase))
            return true;
      }
      return false;
   }

   public static string GetMediaType(string contentType)
   {
      if (string.IsNullOrEmpty(contentType)) return string.Empty;
      var idx = contentType.IndexOf(';');
      return idx >= 0 ? contentType.Substring(0, idx).Trim() : contentType.Trim();
   }
}
