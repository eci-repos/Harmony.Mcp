using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpRpcResponse
{
   [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
   [JsonPropertyName("id")] public string? Id { get; set; }
   [JsonPropertyName("result")] public object? Result { get; set; }
   [JsonPropertyName("error")] public McpError? Error { get; set; }

   public JsonElement? ResultAsJsonElement
   {
      get {
         if (Result is JsonElement je) return je;
         return null;
      }
   }

   public static McpRpcResponse RpcResult(string? id, object result) => new()
   { 
      Id = id, Result = result 
   };
   public static McpRpcResponse RpcError(string? id, int code, string message) => new() 
   { 
      Id = id, Error = new McpError { Code = code, Message = message } 
   
   };
}
