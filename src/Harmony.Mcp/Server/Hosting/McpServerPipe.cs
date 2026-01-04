using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harmony.Mcp.Server.Hosting;

internal class McpServerPipe
{

   public static async Task RunPipeAsync(McpServer server, string pipeName)
   {
      Console.WriteLine($"[server] NamedPipe listening on '{pipeName}'");
      while (true)
      {
         using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
         await pipe.WaitForConnectionAsync();
         Console.WriteLine("[server] Pipe client connected");
         await server.RunOnStreamAsync(pipe, pipe);
         Console.WriteLine("[server] Pipe client disconnected");
      }
   }

}
