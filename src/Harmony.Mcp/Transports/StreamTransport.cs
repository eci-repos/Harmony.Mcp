using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Transports;

public sealed class StreamTransport : StreamBaseTransport, IMcpTransport
{
   private readonly Stream _read;
   private readonly Stream _write;
   private readonly Action? _onDispose;

   public StreamTransport(Stream read, Stream write, Action? onDispose = null)
   {
      _read = read; _write = write; _onDispose = onDispose;
      _reader = new StreamReader(_read, Encoding.UTF8, false, 8192, true);
      _writer = new StreamWriter(_write, Encoding.UTF8, 8192, true) { AutoFlush = true };
   }

   public new void Dispose()
   {
      base.Dispose();
      try { _writer?.Dispose(); } catch { }
      try { _reader?.Dispose(); } catch { }
      _onDispose?.Invoke();
   }

}
