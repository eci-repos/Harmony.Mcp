using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Protocols;

public sealed class HrfMediaTypeMiddleware
{
   public const string CONTENT_LENGTH = "Content-Length:";
   public const string APP_JSON_CONTENT_TYPE = "application/json; charset=utf-8";

   public const string HARMONY_FORMAT_ISHRF = "Harmony.Format.IsHrf";
   public const string HARMONY_FORMAT_ORIGINAL_CONTENT_TYPE = "Harmony.Format.OriginalContentType";
   public const string HARMONY_FORMAT_MEDIA_TYPE = "Harmony.Format.MediaType";
   public const string HARMONY_FORMAT_EXTRACTED_JSON = "Harmony.Format.ExtractedJson";

   private readonly RequestDelegate _next;

   public HrfMediaTypeMiddleware(RequestDelegate next) =>
      _next = next ?? throw new ArgumentNullException(nameof(next));

   public async Task InvokeAsync(HttpContext context)
   {
      if (context == null) throw new ArgumentNullException(nameof(context));

      var originalContentType = context.Request.ContentType ?? string.Empty;
      var mediaType = HrfMediaType.GetMediaType(originalContentType);

      if (HrfMediaType.IsHrfMediaType(mediaType))
      {
         // Read entire request body as UTF-8 text
         using var sr = new StreamReader(context.Request.Body, Encoding.UTF8,
          detectEncodingFromByteOrderMarks: false, leaveOpen: true);
         var raw = await sr.ReadToEndAsync().ConfigureAwait(false);

         // Try to extract an inner framed JSON-RPC payload (headers + \r\n\r\n + body)
         if (TryExtractFramedJson(raw, out var extractedJson))
         {
            // Replace request body with the extracted JSON so downstream handlers see plain JSON
            var bytes = Encoding.UTF8.GetBytes(extractedJson);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            // normalize content type for downstream components
            context.Request.ContentType = APP_JSON_CONTENT_TYPE;

            // Expose HRF metadata for downstream middleware/handlers
            context.Items[HARMONY_FORMAT_ISHRF] = true;
            context.Items[HARMONY_FORMAT_ORIGINAL_CONTENT_TYPE] = originalContentType;
            context.Items[HARMONY_FORMAT_MEDIA_TYPE] = mediaType;
            context.Items[HARMONY_FORMAT_EXTRACTED_JSON] = extractedJson;
         }
         else
         {
            // No framing detected — still mark as HRF so components can perform strict 
            // validation if needed
            context.Items[HARMONY_FORMAT_ISHRF] = true;
            context.Items[HARMONY_FORMAT_ORIGINAL_CONTENT_TYPE] = originalContentType;
            context.Items[HARMONY_FORMAT_MEDIA_TYPE] = mediaType;

            // rewind the body stream so downstream can read it
            var bytes = Encoding.UTF8.GetBytes(raw);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
         }
      }

      await _next(context).ConfigureAwait(false);
   }

   // Attempt to parse framed payloads like:
   // "Content-Length: N\r\nOther-Header: ...\r\n\r\n{ ...json... }"
   // If successful, extractedJson will contain the JSON body portion.
   private static bool TryExtractFramedJson(string raw, out string extractedJson)
   {
      extractedJson = string.Empty;
      if (string.IsNullOrEmpty(raw)) return false;

      // Look for header/body separator (\r\n\r\n). Accept LF-only as fallback.
      var separator = "\r\n\r\n";
      var sepIndex = raw.IndexOf(separator, StringComparison.Ordinal);
      if (sepIndex < 0)
      {
         // try LF-only separator for robustness
         separator = "\n\n";
         sepIndex = raw.IndexOf(separator, StringComparison.Ordinal);
      }
      if (sepIndex < 0)
      {
         // No header/body boundary found — treat raw as body
         return false;
      }

      var headers = raw.Substring(0, sepIndex);
      var body = raw.Substring(sepIndex + separator.Length);

      // Try to find Content-Length header (case-insensitive)
      using var sr = new StringReader(headers);
      string? line;
      int contentLength = -1;
      while ((line = sr.ReadLine()) != null)
      {
         if (line.StartsWith(CONTENT_LENGTH, StringComparison.OrdinalIgnoreCase))
         {
            var val = line.Substring(CONTENT_LENGTH.Length).Trim();
            if (int.TryParse(val, out var n)) contentLength = n;
            break;
         }
      }

      // If Content-Length present, validate body length (in bytes) to avoid partial reads.
      if (contentLength >= 0)
      {
         var bodyByteCount = Encoding.UTF8.GetByteCount(body);
         if (bodyByteCount < contentLength)
         {
            // incomplete body — cannot extract safely
            return false;
         }

         // If body has extra bytes after the declared length, trim to declared length.
         if (bodyByteCount > contentLength)
         {
            // convert to bytes and slice to exact length, then decode back to string
            var bytes = Encoding.UTF8.GetBytes(body);
            var trimmed = Encoding.UTF8.GetString(bytes, 0, contentLength);
            extractedJson = trimmed;
            return true;
         }

         extractedJson = body;
         return true;
      }

      // No Content-Length header found: assume the part after the separator is the JSON body
      extractedJson = body;
      return true;
   }
}

public static class HrfMediaTypeMiddlewareExtensions
{
   public static IApplicationBuilder UseHrfMediaTypes(this IApplicationBuilder app) =>
      app.UseMiddleware<HrfMediaTypeMiddleware>();
}


