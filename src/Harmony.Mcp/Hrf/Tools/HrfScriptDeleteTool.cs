using Harmony.Mcp.Server;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Tools;

internal static class HrfScriptDeleteTool
{
    public static McpTool GetTool(ToolRegistry registry, Dictionary<string, string> scripts)
    {
        return new McpTool(
            name: "hrf.delete",
            description: "Delete a previously registered HRF script by id.",
            inputSchema: JsonDocument.Parse(
               "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}},"
               + "\"required\":[\"id\"]}"),
            handler: async (payload, ct) =>
            {
                try
                {
                    var root = payload.RootElement;
                    var id = root.GetProperty("id").GetString() ?? string.Empty;
                    if (scripts.ContainsKey(id))
                    {
                        scripts.Remove(id);
                        return RequestResult.Okey(new { id, deleted = true });
                    }
                    return RequestResult.Fail($"Script with id '{id}' not found.");
                }
                catch (Exception ex)
                {
                    return RequestResult.Fail("Failed to delete script: " + ex.Message);
                }
            }
        );
    }
}
