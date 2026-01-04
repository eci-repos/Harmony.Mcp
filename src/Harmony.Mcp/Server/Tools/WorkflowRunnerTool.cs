using Harmony.Mcp.Client;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server.Tools;

internal class WorkflowRunnerTool
{

   public static McpTool GetTool(ProviderConfig config, KernelHost kernelHost)
   {
      // Workflow runner tool
      return
          new McpTool(
              name: McpHelper.WorkflowRunToolName,
              description: "Run a named SK workflow with inputs (e.g., draft->summarize).",
              inputSchema: JsonDocument.Parse(@"{
                  ""type"": ""object"",
                  ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""inputs"": { ""type"": ""object"" }
                  },
                  ""required"": [""name""]
                }"),
              handler: async (payload, ct) => await kernelHost.RunWorkflowAsync(payload, ct)
          );
   }

}
