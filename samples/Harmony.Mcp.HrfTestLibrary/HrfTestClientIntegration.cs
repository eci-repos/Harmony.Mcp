using Harmony.Mcp.Transports;
using Harmony.Mcp.Client;
using Harmony.Mcp.Models;
using Harmony.Mcp.Hrf;
using Harmony.Mcp.Hrf.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Harmony.Mcp.HrfTestLibrary;

// -------------------------------------------------------------------------------------------------
// Integration-style test for HRF client interactions.
// The TestInMemoryTransport implements the minimal IMcpTransport contract used by McpClient:
//  - WriteAsync(string, CancellationToken)
//  - ReadAsync(CancellationToken) => Task<(bool ok, string body)>
//  - Dispose()
//
// The transport parses incoming JSON-RPC requests from the client and replies immediately with
// JSON-RPC responses that mimic the server semantics used by McpClient.CallAsync:
// RPC response: { "jsonrpc":"2.0", "id": "<id>", "result": { "result": <toolResult> } }
// For tool calls we return the tool result object under the nested "result" property.
internal class TestInMemoryTransport : IMcpTransport
{
   private readonly Channel<string> _responses = Channel.CreateUnbounded<string>();
   private readonly ConcurrentQueue<string> _requests = new();
   private readonly Dictionary<string, string> _scripts = new(StringComparer.OrdinalIgnoreCase);

   public ValueTask DisposeAsync()
   {
      return default;
   }

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
            // Extract args used in tests
            var idVal = argsEl.GetProperty("id").GetString()!;
            var script = argsEl.GetProperty("script").GetString()!;
            var validateOnly = argsEl.TryGetProperty("validateOnly", out var v) && v.GetBoolean();
            var overwrite = argsEl.TryGetProperty("overwrite", out var o) && o.GetBoolean();

            // Lightweight validation: script must be valid JSON with responses array
            bool valid = true;
            JsonDocument? scriptDoc = null;
            int responseCount = 0;
            string? validationError = null;
            try
            {
               scriptDoc = JsonDocument.Parse(script);
               if (!scriptDoc.RootElement.TryGetProperty("responses", out var responses) ||
                   responses.ValueKind != JsonValueKind.Array)
               {
                  valid = false;
                  validationError = "Missing top-level 'responses' array.";
               }
               else
               {
                  var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                  foreach (var r in responses.EnumerateArray())
                  {
                     if (!r.TryGetProperty("name", out var nm) || 
                        nm.ValueKind != JsonValueKind.String)
                     {
                        valid = false;
                        validationError = "Each response must include a string 'name' property.";
                        break;
                     }
                     var rn = nm.GetString()!;
                     if (!names.Add(rn))
                     {
                        valid = false;
                        validationError = $"Duplicate response name: {rn}";
                        break;
                     }
                  }
                  responseCount = names.Count;
               }
            }
            catch (JsonException ex)
            {
               valid = false;
               validationError = ex.Message;
            }
            finally
            {
               scriptDoc?.Dispose();
            }

            object toolResult;
            if (!valid)
            {
               toolResult = new {
                  id = idVal, validated = false, registered = false, error = validationError
               };
            }
            else if (validateOnly)
            {
               toolResult = new { id = idVal, validated = true, responseCount };
            }
            else
            {
               // Persist script in in-memory store
               if (_scripts.ContainsKey(idVal) && !overwrite)
               {
                  toolResult = new { id = idVal, validated = true, registered = false, error = "exists" };
               }
               else
               {
                  _scripts[idVal] = script;
                  toolResult = new { id = idVal, validated = true, registered = true, responseCount };
               }
            }

            // Build RPC response
            var resp = new
            {
               jsonrpc = "2.0",
               id,
               result = new { result = toolResult }
            };
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

            if (!_scripts.TryGetValue(scriptId, out var scriptText))
            {
               var errResp = new
               {
                  jsonrpc = "2.0",
                  id,
                  result = new { result = new { 
                     ok = false, result = (object?)null, error = $"Unknown script id: {scriptId}" }
                  }
               };
               var errBody = JsonSerializer.Serialize(errResp);
               await _responses.Writer.WriteAsync(errBody, ct);
               return;
            }

            // Find response in script and return its "content"
            try
            {
               using var sd = JsonDocument.Parse(scriptText);
               var rootEl = sd.RootElement;
               var responses = rootEl.GetProperty("responses");
               JsonElement? content = null;
               foreach (var r in responses.EnumerateArray())
               {
                  if (r.GetProperty("name").GetString() == respName)
                  {
                     content = r.GetProperty("content");
                     break;
                  }
               }

               if (!content.HasValue)
               {
                  var notFound = new
                  {
                     jsonrpc = "2.0",
                     id,
                     result = new { result = new { 
                        ok = false, result = (object?)null, error = $"Response '{respName}' not found." }
                     }
                  };
                  var nfBody = JsonSerializer.Serialize(notFound);
                  await _responses.Writer.WriteAsync(nfBody, ct);
                  return;
               }

               // Return the content directly as the tool result (wrapped by nested result)
               var toolResult = new {
                  id = scriptId, 
                  name = respName, 
                  content = JsonDocument.Parse(content.Value.GetRawText()).RootElement 
               };
               var success = new { jsonrpc = "2.0", id, result = new { result = toolResult } };
               var successBody = JsonSerializer.Serialize(success);
               await _responses.Writer.WriteAsync(successBody, ct);
               return;
            }
            catch (JsonException ex)
            {
               var err = new
               {
                  jsonrpc = "2.0",
                  id,
                  result = new { result = new { ok = false, result = (object?)null, error = ex.Message } }
               };
               var errBody = JsonSerializer.Serialize(err);
               await _responses.Writer.WriteAsync(errBody, ct);
               return;
            }
         }
      }

      // Default: return a generic success wrapper
      var defaultResp = new { jsonrpc = "2.0", id, result = new { 
         result = new { ok = true, result = (object?)null, error = (string?)null } }
      };
      await _responses.Writer.WriteAsync(JsonSerializer.Serialize(defaultResp), ct);
   }

   public Task<(bool ok, string body)> ReadAsync(CancellationToken ct = default)
   {
      // Wait for a response to be available
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

public static class HrfTestClientIntegration
{
   public static async Task RunHrfClientIntegrationTestAsync()
   {
      using var transport = new TestInMemoryTransport();
      using var client = new McpClient(default!, transport);
      var hrf = new HrfClient(client);

      // Example HRF script (simple JSON with responses)
      string sampleScript = JsonSerializer.Serialize(new
      {
         hrf_version = "1.0",
         responses = new object[]
         {
            new { name = "summary", content = "This is the summary from HRF." },
            new {
               name = "details",
               content = new {
                  title = "Details",
                  body = "More information here."
               }
            }
         }
      }, McpJson.Options);

      // First validate the script
      var registerArgs = new HrfRegisterArgs
      {
         id = "sample_hfr",
         script = sampleScript,
         contentType = "application/harmony+json",
         validateOnly = true // first validate
      };
      Console.WriteLine("Validating HRF script...");
      var validation = await hrf.RegisterScriptAsync(registerArgs);
      Console.WriteLine($"Validated: id={validation.id} "
         + $"validated={validation.validated} responses={validation.responseCount}");

      // Now register for real (overwrite if present)
      registerArgs.validateOnly = false;
      registerArgs.overwrite = true;
      Console.WriteLine("Registering HRF script...");
      var registration = await hrf.RegisterScriptAsync(registerArgs);
      Console.WriteLine($"Registered: id={registration.id} "
         + $"registered={registration.registered} responses={registration.responseCount}");

      // Call the 'summary' response tool
      Console.WriteLine("Calling response tool: summary");
      var summary = await hrf.CallResponseAsync(registerArgs.id, "summary");
      if (summary.StringContent != null)
      {
         Console.WriteLine($"summary => {summary.StringContent}");
      }
      else if (summary.RawContent.HasValue)
      {
         Console.WriteLine($"summary (json) => {summary.RawContent.Value.GetRawText()}");
      }

      // Call the 'details' response tool
      Console.WriteLine("Calling response tool: details");
      var details = await hrf.CallResponseAsync(registerArgs.id, "details");
      if (details.StringContent != null)
      {
         Console.WriteLine($"details => {details.StringContent}");
      }
      else if (details.RawContent.HasValue)
      {
         Console.WriteLine($"details (json) => {details.RawContent.Value.GetRawText()}");
      }
   }
} 