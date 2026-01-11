using Harmony.Mcp.Models;
using Harmony.Mcp.Transports;
using Harmony.SemanticKernel.Core;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Client;

public sealed class McpClient : IDisposable
{
   private readonly JsonSerializerOptions _jsonSerializerOptions; 
   private readonly IMcpTransport _transport;
   private int _idCounter = 1;
   private readonly 
      ConcurrentDictionary<string, TaskCompletionSource<McpRpcResponse>> _pending = new();
   private readonly CancellationTokenSource _cts = new();


   public McpClient(JsonSerializerOptions jsonSerializerOptions, IMcpTransport transport)
   {
      _jsonSerializerOptions = jsonSerializerOptions; 
      _transport = transport;
      _ = Task.Run(ReadLoopAsync);
   }

   public async Task<McpInitializeResult> InitializeAsync()
   {
      var req = new McpRpcRequest 
      {
         Id = NextId(), Method = "initialize", 
         Params = JsonDocument.Parse("{}").RootElement 
      };
      var resp = await SendAsync(req);

      var result = resp.ResultAsJsonElement;
      return result!.Value.Deserialize<McpInitializeResult>(_jsonSerializerOptions)!;
   }

   public async Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync()
   {
      var req = new McpRpcRequest 
      { 
         Id = NextId(), Method = "tools/list", 
         Params = JsonDocument.Parse("{}").RootElement
      };

      var resp = await SendAsync(req);

      var result = resp.ResultAsJsonElement;
      return result!.Value.GetProperty("tools").
         Deserialize<List<McpToolDescriptor>>(_jsonSerializerOptions)!;
   }

   public async Task<T> CallAsync<T>(string name, JsonElement args)
   {
      var payload = new McpCallToolParams { Name = name, Arguments = args };
      var req = new McpRpcRequest 
      { 
         Id = NextId(), Method = "tools/call", 
         Params = JsonSerializer.SerializeToElement(payload, _jsonSerializerOptions)
      };

      var resp = await SendAsync(req);

      var element = resp.ResultAsJsonElement;
      var result = element!.Value.GetProperty("result");
      return result.Deserialize<T>(_jsonSerializerOptions)!;
   }

   private async Task<McpRpcResponse> SendAsync(
      McpRpcRequest req, CancellationToken ct = default)
   {
      if (req.Id == null)
         throw new ArgumentException("Request ID cannot be null", nameof(req));

      var tcs = new TaskCompletionSource<McpRpcResponse>(
         TaskCreationOptions.RunContinuationsAsynchronously);
      _pending[req.Id] = tcs;

      var json = JsonSerializer.Serialize(req, _jsonSerializerOptions);
      await _transport.WriteAsync(json, ct);

      using (ct.Register(() => tcs.TrySetCanceled()))
         return await tcs.Task.ConfigureAwait(false);
   }

   private async Task ReadLoopAsync()
   {
      try
      {
         while (!_cts.IsCancellationRequested)
         {
            var (ok, body) = await _transport.ReadAsync(_cts.Token);
            if (!ok) break;


            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;


            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null)
            {
               var id = idEl.GetString();
               if (id != null && _pending.TryRemove(id, out var tcs))
               {
                  var resp = JsonSerializer.
                     Deserialize<McpRpcResponse>(body, _jsonSerializerOptions)!;
                  if (resp.Error != null) tcs.TrySetException(
                     new Exception($"RPC error {resp.Error.Code}: {resp.Error.Message}"));
                  else tcs.TrySetResult(resp);
               }
               continue;
            }


            // (Optional) Handle notifications here if server emits any in the future
         }
      }
      catch (Exception ex)
      {
         foreach (var kv in _pending) kv.Value.TrySetException(ex);
      }
   }

   private string NextId() => Interlocked.Increment(ref _idCounter).ToString();

   public void Dispose()
   {
      try { _cts.Cancel(); } catch { }
      _transport.Dispose();
   }

   /// <summary>
   /// Initializes and returns an McpClient based on the provided arguments.
   /// </summary>
   /// <param name="args"></param>
   /// <param name="transport"></param>
   /// <returns></returns>
   /// <exception cref="ArgumentException"></exception>
   public static async Task<McpClient?> GetClient(string[] args, IMcpTransport? transport = null)
   {
      var options = McpJson.Options;
      IMcpTransport mcpTransport;

      // Note that if spawn mode is used, the server must be started separately
      // (e.g. via McpServerProcess.SpawnServerForStdio)
      bool spawnMode = args.Length == 0;
      if (spawnMode || args.Contains("stdio"))
      {
         // spawn server as child process
         //string[] serverArgs = new string[] { "server-stdio" };
         //McpServerProcess.SpawnServerForStdioSameExe(
         //   serverArgs, new Dictionary<string, string?>());

         // prepare stdio transport
         mcpTransport = transport ?? McpTransports.StdioConnect();
      }
      else
      {
         // Connect transport based on args.
         mcpTransport = args[0] switch
         {
            "tcp" when args.Length >= 3 && int.TryParse(args[2], out var p) =>
               await McpTransports.TcpConnectAsync(args[1], p),
            "pipe" when args.Length >= 2 => await McpTransports.NamedPipeConnectAsync(args[1]),
            _ => throw new ArgumentException(
               "Invalid args. Use: tcp <host> <port> | pipe <name> | stdio")
         };
      }

      using var client = new McpClient(McpJson.Options, mcpTransport);

      // Handshake
      var init = await client.InitializeAsync();
      if (init.ServerInfo == null)
      {
         KernelIO.Log.WriteLine("Failed to initialize MCP client: no server info.");
         return null;
      }

      KernelIO.Log.WriteLine(
         $"Initialized: {init.ServerInfo.Name} v{init.ServerInfo.Version} "
        + "(proto {init.ProtocolVersion})\n");

      return client;
   }

}
