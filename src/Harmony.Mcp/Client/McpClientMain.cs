using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Models;
using Harmony.Mcp.Server;
using Harmony.Mcp.Transports;
using OllamaSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Client;

public class McpClientMain
{

   /// <summary>
   /// The entry point of the application. Initializes the MCP client, interacts with the MCP 
   /// server, and demonstrates various tool calls based on the provided arguments.
   /// </summary>
   /// <remarks>This method performs the following operations:
   /// <list type="bullet">
   ///    <item>
   ///       <description>Initializes the MCP client and connects to the server.</description>
   ///    </item>
   ///    <item>
   ///       <description>Lists available tools on the server and displays their descriptions.
   ///       </description>
   ///    </item>
   ///    <item>
   ///       <description>Demonstrates example calls to tools such as "time.now", "math.add", 
   ///          "chat.complete", and "workflow.run" if they are available.</description>
   ///    </item>
   /// </list>
   /// The method uses UTF-8 encoding for console output and supports both "spawn" and "attach" 
   /// modes for interacting with the MCP server.</remarks>
   /// <param name="args">Command-line arguments. The first argument specifies the mode of 
   /// operation, which can be  "spawn" (default) or "attach". Additional arguments are passed to 
   /// the MCP client for processing.</param>
   /// <returns>A task that represents the asynchronous operation.</returns>
   public static async Task<int> Main(string[] args, IMcpTransport? transport)
   {
      var client = await McpClient.GetClient(args, transport);
      if (client == null)
         return 1;

      // Discover tools
      var tools = await client.ListToolsAsync();
      KernelIO.Log.WriteLine("Tools available:");

      foreach (var t in tools)
         KernelIO.Log.WriteLine($" - {t.Name}: {t.Description}");

      KernelIO.Log.WriteLine();

      // Example calls
      var embedDesc = McpHelper.FindEmbeddingsTool(tools);
      if (embedDesc != null)
      {
         var embRes = await McpHelper.EmbeddingsAsync(
             client,
             new EmbeddingsArgs { text = "MCP + SK", texts = null },
             embedDesc // enables light schema validation via McpTyped
         );

         KernelIO.Log.WriteLine(
            $"\nembeddings.embed => count={embRes.count}, dims={embRes.dimensions}");
         if (embRes.embeddings?.Length > 0)
         {
            var first = string.Join(
               ", ", embRes.embeddings[0].Take(8).Select(v => v.ToString("0.###")));
            KernelIO.Log.WriteLine($"first[0..8]: [ {first} ... ]");
         }
      }

      var simDesc = McpHelper.FindSemanticSimilarityTool(tools);
      if (simDesc != null)
      {
         var simReq = new McpSimilarityArgs
         {
            prompt = "Find items about MCP",
            top_k = 3,
            includeEmbeddings = false,
            records = new[] {
               new McpSimilarityRecord {
                  id = "1", text = "Introduction to MCP servers and clients" },
               new McpSimilarityRecord { id = "2", text = "Using Semantic Kernel planners" },
               new McpSimilarityRecord { id = "3", text = "Cooking pasta al dente" }
            }
         };

         var simRes = await McpHelper.SemanticSimilarityAsync(client, simReq, simDesc);
         if (simRes.results == null || simRes.results.Length == 0)
         {
            KernelIO.Log.WriteLine("semantic.similarity => no results");
            return 0;
         }

         KernelIO.Log.WriteLine("\nsemantic.similarity (top results):");
         foreach (var r in simRes.results)
         {
            KernelIO.Log.WriteLine($" - {r.id}: score={r.score:0.000} text='{r.text}'");
         }
      }

      var chat = McpHelper.FindChatCompletionsTool(tools);
      if (chat != null)
      {
         var argsObj = JsonDocument.Parse(
            "{\"prompt\": \"Say hello from the MCP client!\"}").RootElement;
         var resp = await McpHelper.ChatAsync(client, argsObj, chat);
         KernelIO.Log.WriteLine($"chat.complete => {resp}");
      }

      var workflowRun = McpHelper.FindWorkflowRunTool(tools);
      if (workflowRun != null)
      {
         var wfArgs = JsonDocument.Parse(
            "{\n \"name\": \"draft-and-rewrite\",\n \"inputs\": " +
            "{ \"topic\": \"why MCP + SK is neat\", \"style\": \"succinct\" }\n}").RootElement;
         var wf = await client.CallAsync<JsonElement>("workflow.run", wfArgs);
         KernelIO.Log.WriteLine($"workflow.run => {wf}");
      }

      KernelIO.Log.WriteLine("\nDone.");
      return 0;
   }

   /// <summary>
   /// Parses the provided arguments to determine the command, arguments, and environment variables 
   /// for spawning a process.
   /// </summary>
   /// <remarks>The method processes the following options:
   /// <list type="bullet">
   ///    <item>
   ///       <term><c>--command</c></term>
   ///       <description>Specifies the command to execute. Defaults <c>"dotnet"</c> if not provided.
   ///       </description>
   ///    </item> 
   ///    <item>
   ///       <term><c>--args</c></term>
   ///       <description>Specifies the arguments for the command. 
   ///          Defaults to <c>"run --project ../McpSkServer"</c> if not provided.
   ///       </description>
   ///    </item>
   ///    <item>
   ///       <term><c>--env</c></term>
   ///       <description>Specifies environment variables in the format <c>KEY=VALUE</c>.
   ///          Multiple variables can be provided by repeating the <c>--env</c> option.
   ///       </description>
   ///    </item>
   /// </list> 
   /// Any unrecognized options are ignored.</remarks>
   /// <param name="rest">An array of strings representing the arguments to parse. Supported 
   /// options include <c>--command</c>, <c>--args</c>, and <c>--env</c>.</param>
   /// <returns>A tuple containing the following: 
   /// <list type="bullet"> 
   ///    <item>
   ///       <description>The command to execute as a <see cref="string"/>.</description>
   ///    </item> 
   ///    <item>
   ///       <description>The arguments to pass to the command as a <see cref="string"/>.
   ///       </description>
   ///    </item> 
   ///    <item>
   ///       <description>A dictionary of environment variables, where the key is the variable name
   ///          and the value is the variable value.</description>
   ///    </item>
   /// </list>
   /// </returns>
   private static (string cmd, string args, Dictionary<string, string> env) 
      ParseSpawnArgs(string[] rest)
   {
      string cmd = "dotnet";
      var argBuilder = new StringBuilder("run --project ../McpSkServer");
      var env = new Dictionary<string, string>();


      for (int i = 0; i < rest.Length; i++)
      {
         switch (rest[i])
         {
            case "--command":
               cmd = rest[++i];
               break;
            case "--args":
               argBuilder.Clear();
               argBuilder.Append(rest[++i]);
               break;
            case "--env":
               var kv = rest[++i];
               var ix = kv.IndexOf('=');
               if (ix > 0) env[kv[..ix]] = kv[(ix + 1)..];
               break;
            default:
               break;
         }
      }
      return (cmd, argBuilder.ToString(), env);
   }

}
