using Harmony.Mcp.Server;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Harmony.Mcp.Hrf.Tools;

internal static class HrfScriptListTool
{
    public static McpTool GetTool(ToolRegistry registry, Dictionary<string, string> scripts)
    {
        return new McpTool(
            name: "hrf.list",
            description: "List registered HRF script ids.",
            inputSchema: JsonDocument.Parse("{ \"type\": \"object\" }"),
            handler: async (payload, ct) =>
            {
                try
                {
                    // Return a simple list of registered script ids
                    var ids = scripts.Keys.ToArray();
                    return RequestResult.Okey(new { scripts = ids });
                }
                catch (Exception ex)
                {
                    return RequestResult.Fail("Failed to list scripts: " + ex.Message);
                }
            }
        );
    }
}
