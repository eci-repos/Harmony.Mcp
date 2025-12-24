using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

   public static async Task<int> Main(string[] args)
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

      for (int i = 0; i < args.Length; i++)
      {
         switch (args[i])
         {
            case "--tcp": tcpBind = args[++i]; break;
            case "--pipe": pipeName = args[++i]; break;
         }
      }

      var server = new McpServer(kernelHost, jsonOptions, registry);

      if (tcpBind != null)
      {
         await RunTcpAsync(server, tcpBind);
         return 0;
      }
      if (pipeName != null)
      {
         await RunPipeAsync(server, pipeName);
         return 0;
      }

      // Default: single stdio connection

      await server.RunStdIoAsync();

      return 0;
   }

   private static async Task RunTcpAsync(McpServer server, string bind)
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

   private static async Task RunPipeAsync(McpServer server, string pipeName)
   {
      Console.WriteLine($"[server] NamedPipe listening on '{pipeName}'");
      while (true)
      {
         using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
         await pipe.WaitForConnectionAsync();
         Console.WriteLine("[server] Pipe client connected");
         await server.RunOnStreamAsync(pipe, pipe);
         Console.WriteLine("[server] Pipe client disconnected");
      }
   }

   /// <summary>
   /// MCP server entry point for hosting via McpHostProcess.
   /// </summary>
   /// <param name="args">arguments</param>
   public static async Task McpServerRun(string[] args)
   {
      try
      {
         var task = await McpServerMain.Main(args);
      }
      catch (AggregateException ae)
      {
         KernelIO.Error.WriteLine(ae.Flatten().Message);
         Environment.Exit(1);
      }
      catch (Exception ex)
      {
         KernelIO.Error.WriteLine(ex.Message);
         Environment.Exit(1);
      }
   }

}

