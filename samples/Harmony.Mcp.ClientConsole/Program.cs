// See https://aka.ms/new-console-template for more information
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Harmony.Mcp.Server;
using Harmony.Mcp.Transports;
using System.Diagnostics;

// -------------------------------------------------------------------------------------------------


// --- 1) If this is the child/server process, run server and exit ---
if (args.Contains("server-stdio", StringComparer.OrdinalIgnoreCase))
{
   KernelIO.Log.WriteLine("MCP Server (stdio) starting...");
   await McpServerMain.Main(args);
   return 0;
}

// --- 2) Otherwise we are the client host ---
// Modes:
//   (a) stdio/client-stdio => spawn same exe as child in server-stdio mode
//   (b) tcp/pipe/etc.      => your McpClientMain handles it
var useStdio =
    args.Length == 0 ||                          // default to stdio client if no args
    args.Contains("stdio", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("client-stdio", StringComparer.OrdinalIgnoreCase);

if (useStdio)
{
   KernelIO.Log.WriteLine("MCP starting (stdio)...");

   // 2a) Spawn the same executable as a child in server mode.
   //     The child's stdout/stderr are redirected to us by Spawn.
   Process child = McpServerProcess.SpawnServerForStdioSameExe(
       serverArgs: new[] { "server-stdio" },
       env: new Dictionary<string, string?>()
       // add keys if needed, e.g. { "OPENAI_API_KEY", openAiKey }
   );

   try
   {
      // 2b) Attach the client to *our own* stdio (which is pipe-connected to the child).
      // McpClientMain.McpClientRun should use the stdio transport (no extra args required),
      // or you can pass a hint like "client-stdio".
      KernelIO.Log.WriteLine("MCP Client starting (stdio)...");
      using var transport = new ProcessTransport(child);

      await McpClientMain.Main(new[] { "stdio" }, transport);
   }
   catch (Exception ex)
   {
      KernelIO.Log.WriteLine($"MCP Client error: {ex}");
      return 1;
   }
   finally
   {
      // Best-effort cleanup of child process.
      try
      {
         if (!child.HasExited)
         {
            // give the server a moment to flush
            await Task.Delay(100);
            child.Kill(true);
            child.WaitForExit(2000);
         }
      }
      catch { /* ignore */ }
      child.Dispose();
   }

   return 0;
}
else
{
   // Non-stdio client modes (tcp/pipe/etc.) – no spawning; server is external.
   KernelIO.Log.WriteLine("MCP Client starting...");
   _ = await McpClientMain.Main(args, null);
   return 0;
}

