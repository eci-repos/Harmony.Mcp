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
   protected StreamReader? _reader;
   protected StreamWriter? _writer;

   public async Task WriteAsync(string json, CancellationToken ct)
   {
      if (_writer == null)
         throw new InvalidOperationException("Transport is not initialized.");

      await _writer.WriteAsync(
         $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
   }

   public async Task<(bool ok, string body)> ReadAsync(CancellationToken ct)
   {
      if (_reader == null)
         throw new InvalidOperationException("Transport is not initialized.");

      string? line; int contentLength = -1;
      while (!string.IsNullOrEmpty(line = await _reader.ReadLineAsync()))
      {
         if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
         {
            var val = line.Substring("Content-Length:".Length).Trim();
            if (int.TryParse(val, out var n)) contentLength = n;
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
         return (true, new string(buffer, 0, read));
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
