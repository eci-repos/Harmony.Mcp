using Harmony.Mcp.Client;
using Harmony.Mcp.Consoles;
using Harmony.Mcp.HrfTestLibrary;
using Harmony.Mcp.Models;
using Harmony.Mcp.Transports;
using Harmony.Mcp.Hrf;
using Harmony.Mcp.Hrf.Models;
using Harmony.SemanticKernel.Core;
using System.Text.Json;


// -------------------------------------------------------------------------------------------------

var client = new HrfClient();
var result = await McpClientServerMain.StartClientServer(args, client: client);

// 1.0 

// -------------------------------------------------------------------------------------------------
// define client to test HRF management
public class HrfClient : IClientMain
{

   private McpClient? _Client = null;
   public async Task<McpResultLog> Main(string[]? args, IMcpTransport? transport = null)
   {
      McpResultLog log = new McpResultLog();
      if (_Client == null)
      {
         var client = await McpClient.GetClient(
            args == null ? Array.Empty<string>() : args, transport);
         if (client == null)
            return log.Failed();
         _Client = client;
      }

      // Test HRF tool availability and functionality using the sample helpers.
      try
      {
         var scripts = new List<string>() 
         { 
            HrfSamples.SampleDefaultHrfScript, 
            "whatever" 
         };

         // Use the HrfTestLibrary examples to validate/register and call response tools.
         await HrfSamples.RunExampleAsync(_Client);
      }
      catch (Exception ex)
      {
         Console.WriteLine("Error while testing HRF tools: " + ex);
         return log.Failed(message: ex.Message);
      }

      return log.Succeeded();
   }

   public async Task<McpResultLog> ValidateScript(string scriptBody)
   {
      if (_Client == null)
         return new McpResultLog().Failed(message: "Client not initialized.");

      var rawPayload = new
      {
         id = "sample_hrf",
         script = scriptBody,
         contentType = "application/harmony+json",
         validateOnly = true
      };
      var rawArgsEl = JsonSerializer.SerializeToElement(rawPayload, McpJson.Options);

      McpResultLog log = new McpResultLog();
      Console.WriteLine("Validating HRF script (raw CallAsync)...");
      var rawResp = await _Client.CallAsync<JsonElement>("hrf.register", rawArgsEl);
      Console.WriteLine("Raw validation response: " + rawResp.GetRawText());
      return log.Succeeded();
   }

}
