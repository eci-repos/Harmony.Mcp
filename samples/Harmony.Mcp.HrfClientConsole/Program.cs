using Harmony.Mcp.HrfTestLibrary;
using Harmony.Mcp.Client;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var client = await McpClientMain.GetClient(args);
if (client == null)
{
    Console.WriteLine("Failed to create MCP client.");
    return;
}
await HrfSamples.RunExampleAsync(client);
