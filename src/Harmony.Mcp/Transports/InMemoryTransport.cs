using Harmony.Mcp.Hrf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------

namespace Harmony.Mcp.Transports;

/// <summary>
/// Provides an in-memory implementation of the IMcpTransport interface for simulating request and
/// response handling without external dependencies.
/// </summary>
/// <remarks>This transport is intended for testing or local scenarios where network communication 
/// is not required. All requests and responses are processed within the same process, and no 
/// data is persisted beyond the lifetime of the instance. Thread safety is ensured for concurrent
/// read and write operations. Scripts and responses are managed in-memory and are not shared
/// across instances.</remarks>
public class InMemoryTransport : IMcpTransport
{
   private readonly Channel<string> _responses = Channel.CreateUnbounded<string>();
   private readonly Dictionary<string, string> _scripts = new(StringComparer.OrdinalIgnoreCase);

   public void Dispose()
   {
      // nothing to dispose
   }

   public async Task WriteAsync(string json, CancellationToken ct = default)
   {
      // Parse request quickly
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;
      var id = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
         ? idEl.GetString() : Guid.NewGuid().ToString();
      var method = root.GetProperty("method").GetString();

      // Handle only the RPC wrapper that McpClient sends (we only need tools/call)
      if (string.Equals(method, "tools/call", StringComparison.OrdinalIgnoreCase))
      {
         var @params = root.GetProperty("params");
         var name = @params.GetProperty("name").GetString();
         var argsEl = @params.GetProperty("arguments");

         // Handle HRF registration
         if (string.Equals(name, "hrf.register", StringComparison.OrdinalIgnoreCase))
         {
            var toolResult = HrfRegistrationHandler.ProcessRegistration(argsEl, _scripts);

            var resp = new { jsonrpc = "2.0", id, result = new { result = toolResult } };
            var body = JsonSerializer.Serialize(resp);
            await _responses.Writer.WriteAsync(body, ct);
            return;
         }

         // Handle per-response tool calls: name = "{id}.{responseName}"
         if (!string.IsNullOrEmpty(name) && name.Contains('.'))
         {
            var dot = name.IndexOf('.');
            var scriptId = name[..dot];
            var respName = name[(dot + 1)..];

            var (ok, toolResult, error) = HrfResponseHandler.BuildPerResponseResult(
               scriptId, respName, _scripts);

            if (!ok)
            {
               var errResp = new
               {
                  jsonrpc = "2.0",
                  id,
                  result = new { result = new { ok = false, result = (object?)null, error } }
               };
               var errBody = JsonSerializer.Serialize(errResp);
               await _responses.Writer.WriteAsync(errBody, ct);
               return;
            }

            var success = new { jsonrpc = "2.0", id, result = new { result = toolResult } };
            var successBody = JsonSerializer.Serialize(success);
            await _responses.Writer.WriteAsync(successBody, ct);
            return;
         }
      }

      var defaultResp = new
      {
         jsonrpc = "2.0",
         id,
         result = new { result = new { ok = true, result = (object?)null, error = (string?)null } }
      };
      await _responses.Writer.WriteAsync(JsonSerializer.Serialize(defaultResp), ct);
   }

   public Task<(bool ok, string body)> ReadAsync(CancellationToken ct = default)
   {
      return WaitForResponseAsync(ct);
   }

   private async Task<(bool ok, string body)> WaitForResponseAsync(CancellationToken ct)
   {
      try
      {
         var body = await _responses.Reader.ReadAsync(ct);
         return (true, body);
      }
      catch (OperationCanceledException) { return (false, string.Empty); }
   }
}

