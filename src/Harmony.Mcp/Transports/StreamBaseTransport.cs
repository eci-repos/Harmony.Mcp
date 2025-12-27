using Harmony.Mcp.Server.Protocols;
using Harmony.SemanticKernel.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Transports;

public class StreamBaseTransport : IMcpTransport
{
   public const string CONTENT_LENGTH_HEADER = "Content-Length:";

   protected StreamReader? _reader;
   protected StreamWriter? _writer;

   private static void ValidateInitialized(object? stream)
   {
      if (stream == null)
         throw new InvalidOperationException("Transport is not initialized.");
   }

   public async Task WriteAsync(string json, CancellationToken ct)
   {
      ValidateInitialized(_writer);

      await _writer!.WriteAsync(
         $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
   }

   public async Task<(bool ok, string body)> ReadAsync(CancellationToken ct)
   {
      ValidateInitialized(_reader);

      string? line; int contentLength = -1;
      string? contentTypeHeader = null;
      while (!string.IsNullOrEmpty(line = await _reader!.ReadLineAsync()))
      {
         if (line.StartsWith(CONTENT_LENGTH_HEADER, StringComparison.OrdinalIgnoreCase))
         {
            var val = line.Substring(CONTENT_LENGTH_HEADER.Length).Trim();
            if (int.TryParse(val, out var n)) contentLength = n;
         }
         else if (line.StartsWith(CONTENT_LENGTH_HEADER, StringComparison.OrdinalIgnoreCase))
         {
            contentTypeHeader = line.Substring(CONTENT_LENGTH_HEADER.Length).Trim();
         }
      }
      if (contentLength < 0) return (false, string.Empty);

      char[] buffer = ArrayPool<char>.Shared.Rent(contentLength);
      try
      {
         int read = 0;
         while (read < contentLength)
         {
            int r = await _reader.ReadAsync(buffer.AsMemory(read, contentLength - read), ct);
            if (r == 0) break;
            read += r;
         }

         var body = new string(buffer, 0, read);

         if (!HrfValidator.ValidateContentTypeHeader(contentTypeHeader, body))
         {
            KernelIO.Error.WriteLine(
               $"[transport] Content-Type header validation failed: {contentTypeHeader}");
            return (false, string.Empty);
         }

         return (true, body);
      }
      finally
      {
         ArrayPool<char>.Shared.Return(buffer);
      }
   }

   public void Dispose()
   {
      try { _writer?.Dispose(); } catch { }
      try { _reader?.Dispose(); } catch { }
   }

}
