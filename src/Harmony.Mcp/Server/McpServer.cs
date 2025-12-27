using Harmony.Mcp.Models;
using Harmony.SemanticKernel.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Harmony.Mcp.Server.Protocols;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server;


public sealed class McpServer
{
   private readonly JsonSerializerOptions _jsonSerializerOptions;
   private readonly ToolRegistry _registry;
   public KernelHost _kernelHost;

   /// <summary>
   /// Initializes a new instance of the <see cref="McpServer"/> class with the specified JSON 
   /// serializer options and tool registry.
   /// </summary>
   /// <remarks>Use this constructor to create an instance of <see cref="McpServer"/> with custom 
   /// serialization settings and a specific tool registry.</remarks>
   /// <param name="kernelHost">The <see cref="KernelHost"/> that provides access to the semantic
   /// </param>
   /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> used to configure
   /// JSON serialization and deserialization.</param>
   /// <param name="registry">The <see cref="ToolRegistry"/> that provides access to registered 
   /// tools.</param>
   public McpServer(
      KernelHost kernelHost, JsonSerializerOptions jsonSerializerOptions, ToolRegistry registry)
   { 
      _kernelHost = kernelHost;
      _jsonSerializerOptions = jsonSerializerOptions; 
      _registry = registry; 
   }

   // STDIO single-session
   public async Task RunStdIoAsync(CancellationToken ct = default)
   {
      try
      {
         using var reader = new StreamReader(
            Console.OpenStandardInput(), Console.InputEncoding, false, 8192, true);
         using var writer = new StreamWriter(
            Console.OpenStandardOutput(), Console.OutputEncoding, 8192, true)
         { AutoFlush = true };

         KernelIO.Error.WriteLine("[server] Listening...");
         await RunLoopAsync(reader, writer, ct);
      }
      catch(Exception ex)
      {
         KernelIO.Error.WriteLine("[RunStdIoAsync] " + ex.Message);
      }
   }

   // Run on arbitrary streams (TCP, Pipes)
   public async Task RunOnStreamAsync(Stream read, Stream write, CancellationToken ct = default)
   {
      using var reader = new StreamReader(read, Encoding.UTF8, false, 8192, true);
      using var writer = new StreamWriter(write, Encoding.UTF8, 8192, true) { AutoFlush = true };
      await RunLoopAsync(reader, writer, ct);
   }

   private async Task RunLoopAsync(StreamReader reader, StreamWriter writer, CancellationToken ct)
   {
      while (!ct.IsCancellationRequested)
      {
         var (ok, body) = await ReadFramedAsync(reader, ct);
         if (!ok) break; // EOF

         McpRpcResponse? resp = null;
         try
         {
            var req = JsonSerializer.Deserialize<McpRpcRequest>(body, _jsonSerializerOptions)!;
            resp = await HandleAsync(req, ct);
         }
         catch (Exception ex)
         {
            string? id = null;
            try 
            { 
               id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetString(); 
            } 
            catch (Exception innerEx)
            { 
               KernelIO.Error.WriteLine($"[server] Error parsing request ID: {innerEx.Message}");
            }
            resp = McpRpcResponse.RpcError(id, -32603, $"Internal error: {ex.Message}");
         }

         if (resp != null)
         {
            var payload = JsonSerializer.Serialize(resp, _jsonSerializerOptions);
            await WriteFramedAsync(writer, payload, ct);
         }
      }
   }

   /// <summary>
   /// Handles an incoming RPC request asynchronously and returns the appropriate response.
   /// </summary>
   /// <remarks>This method processes various RPC methods, including:
   /// <list type="bullet">
   ///    <item>
   ///       <description><c>initialize</c>: Returns server information, protocol version, and 
   ///          capabilities.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>tools/list</c>: Returns a list of available tools.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>tools/call</c>: Invokes a tool with the specified arguments and 
   ///          returns the result or an error.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>ping</c>: Responds with a simple acknowledgment.</description>
   ///    </item>
   /// </list>
   /// If the method specified in the request is not recognized, an error response is returned 
   /// with the code <c>-32601</c>.
   /// </remarks>
   /// <param name="req">The RPC request to process, containing the method name and any associated
   /// parameters.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.
   /// </param>
   /// <returns>A task that represents the asynchronous operation. The task result is an
   /// <see cref="McpRpcResponse"/> containing the result of the requested method, or an error 
   /// response if the method is not found or fails.</returns>
   private async Task<McpRpcResponse> HandleAsync(McpRpcRequest req, CancellationToken ct)
   {
      switch (req.Method)
      {
         case "initialize":
            var init = new McpInitializeResult
            {
               ProtocolVersion = "2024-11-05",
               ServerInfo = new McpServerInfo { Name = "mcp-sk-server", Version = "0.2.0" },
               Capabilities = new McpCapabilities
               {
                  Tools = new McpToolingCapability { ListChanged = true }
               }
            };
            return McpRpcResponse.RpcResult(req.Id, init);

         case "tools/list":
            var tools = _registry.ListTools();
            return McpRpcResponse.RpcResult(req.Id, new { tools });

         case "tools/call":
            {
               JsonElement? v = req.Params!;
               var call = JsonSerializer.
                  Deserialize<McpCallToolParams>((JsonElement)v, _jsonSerializerOptions)!;

               if (String.IsNullOrWhiteSpace(call.Name))
               {
                  return McpRpcResponse.RpcError(
                     req.Id, -32000, $"Tool not found: {call.Name}");
               }

               var (ok, result, error) = 
                  await _registry.TryCallAsync(call.Name, call.Arguments, ct);
               if (ok) 
                  return McpRpcResponse.RpcResult(req.Id, new { result });
               return McpRpcResponse.RpcError(req.Id, -32001, error ?? "Tool error");
            }

         case "ping":
            return McpRpcResponse.RpcResult(req.Id, new { pong = true });
      }

      return McpRpcResponse.RpcError(req.Id, -32601, $"Method not found: {req.Method}");
   }

   /// <summary>
   /// Reads a framed message from the specified <see cref="StreamReader"/> asynchronously, 
   /// extracting the message body based on the "Content-Length" header.
   /// </summary>
   /// <remarks>This method expects the input stream to contain HTTP-like headers, including a 
   /// "Content-Length" header  specifying the size of the message body in bytes. The method reads 
   /// the headers, determines the content length,  and then reads the specified number of
   /// characters from the stream. If the "Content-Length" header is missing  or invalid, the 
   /// method returns <c>(false, string.Empty)</c>.</remarks>
   /// <param name="reader">The <see cref="StreamReader"/> to read the framed message from.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> to observe while waiting for the 
   /// operation to complete.</param>
   /// <returns>A tuple containing a boolean and a string:
   /// <list type="bullet">
   ///    <item>
   ///       <description><c>ok</c>: <see langword="true"/> if a valid message body was
   ///          successfully read; otherwise, <see langword="false"/>.</description>
   ///    </item>
   ///    <item>
   ///       <description><c>body</c>: The message body as a string, or an empty string if no
   ///          valid message was read.</description>
   ///    </item> 
   /// </list>
   /// </returns>
   private static async Task<(bool ok, string body)> ReadFramedAsync(
      StreamReader reader, CancellationToken ct)
   {
      // Expect headers like: Content-Length: N\r\n...\r\n\r\n
      string? line;
      int contentLength = -1;
      string? contentTypeHeader = null;

      while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
      {
         if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
         {
            var val = line.Substring("Content-Length:".Length).Trim();
            if (int.TryParse(val, out var n)) contentLength = n;
         }
         else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
         {
            contentTypeHeader = line.Substring("Content-Type:".Length).Trim();
         }
         // ignore other headers
      }

      if (contentLength < 0)
      {
         // no more input
         return (false, string.Empty);
      }

      char[] buffer = ArrayPool<char>.Shared.Rent(contentLength);
      try
      {
         int read = 0;
         while (read < contentLength)
         {
            int r = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), ct);
            if (r == 0) break;
            read += r;
         }
         var body = new string(buffer, 0, read);

         if (!HrfValidator.ValidateContentTypeHeader(contentTypeHeader, body))
         {
            KernelIO.Error.WriteLine(
               $"[server] Invalid Content-Type header: {contentTypeHeader}");
            return (false, string.Empty);
         }

         return (true, body);
      }
      finally
      {
         ArrayPool<char>.Shared.Return(buffer);
      }
   }

   /// <summary>
   /// Writes a JSON string to the specified <see cref="StreamWriter"/> with a content-length 
   /// header.
   /// </summary>
   /// <remarks>The method writes the JSON string prefixed by a "Content-Length" header, which 
   /// specifies the byte length of the JSON string when encoded in UTF-8. The header and JSON 
   /// content are separated by two CRLF sequences.</remarks>
   /// <param name="writer">The <see cref="StreamWriter"/> to which the framed JSON string will be
   /// written. Cannot be <c>null</c>.</param>
   /// <param name="json">The JSON string to write. Cannot be <c>null</c> or empty.</param>
   /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.
   /// </param>
   /// <returns>A task that represents the asynchronous write operation.</returns>
   private static async Task WriteFramedAsync(
      StreamWriter writer, string json, CancellationToken ct)
   {
      await writer.WriteAsync($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
   }

   public async Task<RequestResult> RunWorkflowAsync(JsonDocument payload, CancellationToken ct)
   {
      var root = payload.RootElement;
      var name = root.GetProperty("name").GetString()!;
      var inputs = root.TryGetProperty("inputs", out var inp) ? inp : default;

      switch (name)
      {
         case "draft-and-rewrite":
            var topic = inputs.TryGetProperty("topic", out var topicEl) ? 
               topicEl.GetString() : "a note";
            var style = inputs.TryGetProperty("style", out var styleEl) ? 
               styleEl.GetString() : "concise and friendly";

            var chat = _kernelHost.GetChatService();

            var settings = new OpenAIPromptExecutionSettings
            {
               MaxTokens = 300,
               Temperature = 0.2,
               TopP = 0.9,
               PresencePenalty = 0,
               FrequencyPenalty = 0,
               StopSequences = new[] { "\n\n" }
            };

            var history = new ChatHistory();
            history.AddSystemMessage($"Write a short draft about the topic in a {style} style.");
            history.AddUserMessage(topic!);
            var draft = await chat.GetChatMessageContentAsync(
               history, settings, _kernelHost.Instance, ct);

            var rewriter = _kernelHost.GetFunction("writing", "rewrite");
            if (rewriter == null)
            {
               return RequestResult.Fail("Workflow error: 'rewrite' function not found");
            }
            var rewritten = await _kernelHost.Instance.InvokeAsync(
               rewriter, new() { ["input"] = draft.Content ?? string.Empty }, ct);

            return RequestResult.Okey(new { draft = draft.Content, final = rewritten.ToString() });

         default:
            return RequestResult.Fail($"Unknown workflow: {name}");
      }
   }

}

