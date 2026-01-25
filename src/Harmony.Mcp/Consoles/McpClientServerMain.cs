using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Harmony.Mcp.Server;
using Harmony.Mcp.Transports;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Consoles;

public class McpClientServerMain
{
   public const string MCP_SERVER_STDIO = "server-stdio";
   public const string MCP_CLIENT_STDIO = "client-stdio";

   public McpClientServerMain(string[]? args, IMcpTransport? transport = null)
   {

   }

   public static Process StartServerStdio(string[] args)
   {

      // Spawn the same executable as a child in server mode.
      //    The child's stdout/stderr are redirected to us by Spawn.
      Process server = McpServerProcess.SpawnServerForStdioSameExe(
          serverArgs: args,
          env: new Dictionary<string, string?>()
      // add keys if needed, e.g. { "OPENAI_API_KEY", openAiKey }
      );

      return server;
   }

   public static async Task<McpResultLog> StartClientStdio(
      Process server, IClientMain client)
   {
      McpResultLog resultLog = McpResultLog.Suceeded();

      try
      {
         // 2b) Attach the client to *our own* stdio (which is pipe-connected to the child).
         // McpClientMain.McpClientRun should use the stdio transport (no extra args required),
         // or you can pass a hint like "client-stdio".
         KernelIO.Log.WriteLine("MCP Client starting (stdio)...");
         using var transport = new ProcessTransport(server);

         await client.Main(new[] { "stdio" }, transport);
         resultLog = McpResultLog.Suceeded();
      }
      catch (Exception ex)
      {
         resultLog = McpResultLog.Suceeded(1, $"MCP Client error: {ex}");
      }
      finally
      {
         // Best-effort cleanup of child process.
         try
         {
            if (!server.HasExited)
            {
               // give the server a moment to flush
               await Task.Delay(100);
               server.Kill(true);
               server.WaitForExit(2000);
            }
         }
         catch { /* ignore */ }
         server.Dispose();
      }

      return resultLog;
   }

   /// <summary>
   /// Start Server and Client.  Both needs to be started independently.
   /// </summary>
   /// <param name="args"></param>
   /// <param name="transport"></param>
   /// <param name="client"></param>
   /// <returns>McpResultLog instance is returned</returns>
   public static async Task<McpResultLog> StartClientServer(
      string[] args, IMcpTransport? transport = null, IClientMain? client = null)
   {

      // --- 1) If this is the child/server process, run server and exit ---
      if (args.Contains(MCP_SERVER_STDIO, StringComparer.OrdinalIgnoreCase))
      {
         KernelIO.Log.WriteLine("MCP Server (stdio) starting...");
         await McpServerMain.Main(args);
         return McpResultLog.Suceeded();
      }

      // --- 2) Otherwise we are the client host ---
      // Modes:
      //   (a) stdio/client-stdio => spawn same exe as child in server-stdio mode
      //   (b) tcp/pipe/etc.      => your McpClientMain handles it
      var useStdio =
          args.Length == 0 ||                          // default to stdio client if no args
          args.Contains("stdio", StringComparer.OrdinalIgnoreCase) ||
          args.Contains(MCP_CLIENT_STDIO, StringComparer.OrdinalIgnoreCase);

      if (useStdio)
      {
         Process server = StartServerStdio(new[] { MCP_SERVER_STDIO });
         if (client != null)
         {
            await StartClientStdio(server, client);
         }
      }
      else
      {
         // Non-stdio client modes (tcp/pipe/etc.) – no spawning; server is external.
         KernelIO.Log.WriteLine("MCP Client starting...");
         if (client != null)
         {
            _ = await client.Main(args, null);
         }
         return McpResultLog.Suceeded();
      }

      return McpResultLog.Suceeded();
   }

}
