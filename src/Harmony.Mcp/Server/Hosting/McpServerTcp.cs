using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Harmony.Mcp.Server.Hosting;

internal class McpServerTcp
{

   public static async Task RunTcpAsync(McpServer server, string bind)
   {
      // bind format: ":51377" or "127.0.0.1:51377"
      var parts = bind.Split(':', StringSplitOptions.RemoveEmptyEntries);
      IPAddress ip = IPAddress.Loopback; int port;
      if (parts.Length == 1)
      {
         port = int.Parse(parts[0]);
      }
      else
      {
         ip = IPAddress.Parse(parts[0]);
         port = int.Parse(parts[1]);
      }

      var listener = new TcpListener(ip, port);
      listener.Start();
      Console.WriteLine($"[server] TCP listening on {ip}:{port}");

      while (true)
      {
         var client = await listener.AcceptTcpClientAsync();
         _ = Task.Run(async () =>
         {
            using var stream = client.GetStream();
            await server.RunOnStreamAsync(stream, stream);
            client.Close();
         });
      }
   }

}
