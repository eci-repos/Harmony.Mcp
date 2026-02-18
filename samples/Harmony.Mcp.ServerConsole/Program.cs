// See https://aka.ms/new-console-template for more information
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Server;

// -------------------------------------------------------------------------------------------------

// see the Mcp.ClientConsole for an example of how to start this server as a child process from a
// client console app
KernelIO.Console.WriteLine("MCP Server starting...");

var result = await McpServerMain.McpServerRun(args);

return result.Code;
