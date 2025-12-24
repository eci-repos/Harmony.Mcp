// See https://aka.ms/new-console-template for more information
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Server;

KernelIO.Console.WriteLine("MCP Server starting...");

await McpServerMain.McpServerRun(args);


