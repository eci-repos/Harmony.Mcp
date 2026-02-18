using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Transports;

/// <summary>
/// Process-based transport for MCP communication over standard input/output streams.
/// </summary>
/// <remarks>Use this transport when you spawn the child server process.</remarks>
public class ProcessTransport : StreamBaseTransport, IMcpTransport
{
   private readonly Process _proc;

   public ProcessTransport(Process proc)
   {
      _proc = proc;
      _reader = new StreamReader(proc.StandardOutput.BaseStream, Encoding.UTF8, 
         detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
      _writer = new StreamWriter(proc.StandardInput.BaseStream, Encoding.UTF8, 
         bufferSize: 8192, leaveOpen: true) { AutoFlush = true };
   }

   public new void Dispose()
   {
      try 
      { 
         _writer?.Flush(); 
      } 
      catch (Exception ex)
      { 

      }
      // Do not dispose proc streams here; the Process owns them.
      // Optionally terminate child, if that’s your lifecycle policy.
   }
}
