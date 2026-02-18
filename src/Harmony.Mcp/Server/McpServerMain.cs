
using Harmony.Mcp.Models;
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Server.Hosting;

using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server;

/// <summary>
/// Represents the entry point for the Managed Cognitive Processing (MCP) application.
/// </summary>
/// <remarks>This class initializes the necessary components for the MCP application, including the 
/// kernel host, tool registry, and server. It registers various AI-centric tools for tasks such as
/// generating embeddings, computing semantic similarity, chat completions, and running workflows. 
/// The application is designed to process input and output in UTF-8 encoding and operates as a 
/// server using standard input/output (stdio).</remarks>
public class McpServerMain
{

   /// <summary>
   /// Main entry point for the MCP Server application. Initializes the kernel host, registers 
   /// AI-centric tools, and starts the server based on provided arguments for transport options 
   /// (TCP, named pipe, HTTPS/HTTP). Defaults to a single stdio connection if no transport arguments
   /// are provided.
   /// </summary>
   /// <param name="args"></param>
   /// <returns></returns>
   public static async Task<McpResultLog> Main(string[] args)
   {
      Console.OutputEncoding = Encoding.UTF8;
      Console.InputEncoding = Encoding.UTF8;

      var jsonOptions = McpJson.Options;
      var config = new ProviderConfig(needEmbeddings: true);

      // Initialize SK
      var kernelHost = await KernelHost.PrepareKernelHostAsync(config);
      kernelHost.GetRewriteFunction();

      // Register AI-centric tools
      var registry = ToolRegistry.BuildTools(jsonOptions, kernelHost);

      // Parse args for transport(s) [default: stdio]
      // Examples:
      //   (no args) -> STDIO server
      //   --tcp :51377
      //   --tcp 127.0.0.1:51377
      //   --pipe mcp-sk-pipe
      string? tcpBind = null;
      string? pipeName = null;
      string? httpsBind = null;

      for (int i = 0; i < args.Length; i++)
      {
         switch (args[i])
         {
            case "--tcp": tcpBind = args[++i]; break;
            case "--pipe": pipeName = args[++i]; break;
            case "--https": // HTTPS transport
               httpsBind = (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                   ? args[++i]
                   : "https://localhost:5001";
               break;
            case "--http": // HTTP transport (for dev/testing)
               httpsBind = (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                   ? args[++i]
                   : "http://localhost:5000";
               break;
         }
      }

      var server = new McpServer(kernelHost, jsonOptions, registry);

      McpResultLog result = new McpResultLog();
      try
      {
         // HTTPS/HTTP mode
         if (httpsBind != null)
         {
            await McpServerHttp.RunHttpAsync(server, httpsBind, jsonOptions);
         }
         if (tcpBind != null)
         {
            await McpServerTcp.RunTcpAsync(server, tcpBind);
         }
         if (pipeName != null)
         {
            await McpServerPipe.RunPipeAsync(server, pipeName);
         }

         // Default: single stdio connection
         await server.RunStdIoAsync();
      }      
      catch (Exception ex)
      {
         KernelIO.Error.WriteLine($"Error starting MCP Server: {ex.Message}");
         return result.Failed(message: $"Failed to start server: {ex.Message}");
      }

      return result.Succeeded();
   }

   /// <summary>
   /// MCP server entry point for hosting via McpHostProcess.
   /// </summary>
   /// <param name="args">arguments</param>
   public static async Task<McpResultLog> McpServerRun(string[] args)
   {
      return await McpServerMain.Main(args);
   }

}

