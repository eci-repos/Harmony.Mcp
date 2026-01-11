using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Transports;

public static class McpTransports
{

   public static async Task<IMcpTransport> TcpConnectAsync(string host, int port)
   {
      var client = new TcpClient();
      await client.ConnectAsync(host, port);
      var stream = client.GetStream();
      return new StreamTransport(stream, stream, onDispose: () => client.Dispose());
   }

   public static async Task<IMcpTransport> NamedPipeConnectAsync(
      string pipeName, string serverName = ".")
   {
      var pipe = new NamedPipeClientStream(
         serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
      await pipe.ConnectAsync(10000);
      return new StreamTransport(pipe, pipe);
   }

   public static IMcpTransport StdioConnect()
   {
      var input = Console.OpenStandardInput();
      var output = Console.OpenStandardOutput();
      var read = new BufferedStream(input, 8192);
      var write = new BufferedStream(output, 8192);
      return new StreamTransport(read, write);
   }

   public static IMcpTransport CreateInMemoryTransport()
   {
      return new InMemoryTransport();
   }

}
