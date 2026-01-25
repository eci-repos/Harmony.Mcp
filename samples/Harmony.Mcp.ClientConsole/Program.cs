// See https://aka.ms/new-console-template for more information
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Harmony.Mcp.Server;
using Harmony.Mcp.Transports;
using System.Diagnostics;
using Harmony.Mcp.Consoles;

// -------------------------------------------------------------------------------------------------

var result = await McpClientServerMain.StartClientServer(args, null, new McpClientMain());


