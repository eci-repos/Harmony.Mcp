using Harmony.Mcp.Models;
using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server;

/// <summary>
/// Represents a tool with a name, description, input schema, and a handler for processing requests.
/// </summary>
/// <remarks>The <see cref="McpTool"/> class encapsulates the metadata and behavior of a tool, 
/// including its name, description, input schema, and a handler function that processes requests 
/// asynchronously. Instances of this class are immutable after construction.</remarks>
public sealed class McpTool
{
   public string Name { get; }
   public string Description { get; }
   public JsonDocument InputSchemaDoc { get; }
   public Func<JsonDocument, CancellationToken, Task<RequestResult>> Handler { get; }

   public McpTool(string name, string description, JsonDocument inputSchema,
      Func<JsonDocument, CancellationToken, Task<RequestResult>> handler)
   {
      Name = name; Description = description; InputSchemaDoc = inputSchema; Handler = handler;
   }

   public McpToolDescriptor Descriptor => new()
   {
      Name = Name,
      Description = Description,
      InputSchema = JsonDocument.Parse(InputSchemaDoc.RootElement.GetRawText()).RootElement
   };
}

