using Harmony.Mcp.Server;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Tools;

/// <summary>
/// Provides a factory for creating a tool that retrieves a previously registered HRF script by its 
/// identifier from the specified tool registry.
/// </summary>
/// <remarks>The tool created by this class expects an input object containing an "id" property, 
/// which specifies the identifier of the script to retrieve. If the script is found in the 
/// provided scripts dictionary, the tool returns the script along with its identifier; otherwise,
/// it returns a failure message. The handler is asynchronous and may return error information 
/// if retrieval fails.</remarks>
internal static class HrfScriptGetItemTool
{
    public static McpTool GetTool(ToolRegistry registry, Dictionary<string, string> scripts)
    {
        return new McpTool(
            name: "hrf.get",
            description: "Retrieve a previously registered HRF script by id.",
            inputSchema: JsonDocument.Parse(
               "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}},"
             + "\"required\":[\"id\"]}"),
            handler: async (payload, ct) =>
            {
                try
                {
                    var root = payload.RootElement;
                    var id = root.GetProperty("id").GetString() ?? string.Empty;
                    if (scripts.TryGetValue(id, out var script))
                    {
                        return RequestResult.Okey(new { id, script });
                    }
                    return RequestResult.Fail($"Script with id '{id}' not found.");
                }
                catch (Exception ex)
                {
                    return RequestResult.Fail("Failed to retrieve script: " + ex.Message);
                }
            }
        );
    }
}
