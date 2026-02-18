// See https://aka.ms/new-console-template for more information
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Harmony.Mcp.Server;
using Harmony.Mcp.Transports;
using System.Diagnostics;
using Harmony.Mcp.Consoles;

// -------------------------------------------------------------------------------------------------
// Note that starting the client with "stdio" or "client-stdio" is just a hint to McpClientMain
// that it should use stdio transport (which is what McpClientServerMain will set up for it).
// You can also start the client with no args and McpClientMain will default to stdio transport
// if it detects it's connected to a pipe/stdio.

// In stdio mode, McpClientServerMain will spawn a child process with the same exe and
// "server-stdio" arg, and connect its stdio to the child. This is awesome for testing and
// debugging, since you can just run the same exe in "server-stdio" mode to run the server
// standalone, and you can put breakpoints in both server and client code in the same
// project/solution.

McpResultLog result;
if (args.Contains(McpClientServerMain.MCP_SERVER_STDIO, StringComparer.OrdinalIgnoreCase))
{
   var msg = $"MCP Server (stdio) starting with args: {string.Join(' ', args)}";
   KernelIO.Log.WriteLine(msg);
   result = await McpServerMain.Main(args);
}
else
{
   var msg = $"MCP Client (stdio) starting with args: {string.Join(' ', args)}";
   KernelIO.Log.WriteLine(msg);
   result = await McpClientServerMain.StartClientServer(args, null, new McpClientMain());
}

return result.Code;



