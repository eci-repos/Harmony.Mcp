using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server.Protocols;

internal static class HrfValidator
{

   /// <summary>
   /// Perform lightweight HRF validation on the JSON body according to the declared HRF variant.
   /// - Ensures the body is valid JSON.
   /// - For the "harmony-script" variant, requires presence of typical script keys: "vars" or 
   ///   "steps"
   /// - For general harmony+json, requires at least one expected HRF envelope key: "contentType",
   ///   "messages", or "vars"
   /// Validation is intentionally conservative to avoid false positives; extend rules if you
   /// have a formal spec.
   /// </summary>
   public static bool ValidateJsonForMediaType(string contentType, string body)
   {
      try
      {
         using var doc = JsonDocument.Parse(body);
         var root = doc.RootElement;

         var media = HrfMediaType.NormalizeMediaType(contentType);

         if (string.Equals(media, HrfMediaType.HRF_SCRIPT_MEDIA_TYPE, StringComparison.OrdinalIgnoreCase))
         {
            if (root.TryGetProperty("vars", out _) || root.TryGetProperty("steps", out _))
               return true;
            if (root.TryGetProperty("contentType", out var ct) &&
                ct.ValueKind == JsonValueKind.String &&
                ct.GetString()?.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0)
               return true;

            return false;
         }

         // application/harmony+json (generic)
         if (root.TryGetProperty("contentType", out _) || 
             root.TryGetProperty("messages", out _) || root.TryGetProperty("vars", out _))
            return true;

         // As a final fallback accept objects that look like JSON-RPC
         // (have "jsonrpc" and "method" or "id")
         if (root.TryGetProperty("jsonrpc", out _) &&
             (root.TryGetProperty("method", out _) || root.TryGetProperty("id", out _)))
            return true;

         return false;
      }
      catch (JsonException)
      {
         return false;
      }
      catch (Exception)
      {
         return false;
      }
   }

   /// <summary>
   /// Validate the Content-Type header and body for HRF compliance if applicable.
   /// </summary>
   /// <param name="contentTypeHeader">content type header</param>
   /// <param name="body">body</param>
   /// <returns>true if valid</returns>
   public static bool ValidateContentTypeHeader(string? contentTypeHeader, string? body)
   {
      // If Content-Type indicates HRF, perform conservative validation
      if (!string.IsNullOrEmpty(contentTypeHeader) &&
         HrfMediaType.IsHrfContentMediaType(contentType: contentTypeHeader))
      {
         if (body == null || !ValidateJsonForMediaType(contentTypeHeader, body))
         {
            KernelIO.Error.WriteLine(
               $"Invalid HRF payload for Content-Type: {contentTypeHeader}");
            return false;
         }
      }
      return true;
   }

}
