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

// -------------------------------------------------------------------------------------------------

namespace Harmony.Mcp.HrfTestLibrary;

/// <summary>
/// Provides an integration test for the HRF client, demonstrating script validation, registration,
/// and response invocation using in-memory transport.
/// </summary>
/// <remarks>This class is intended for use in test scenarios to verify the end-to-end functionality
/// of the HRF client API. It showcases how to validate and register an HRF script, and how to 
/// invoke response tools defined within the script. The integration test uses in-memory transport
/// to avoid external dependencies and is not intended for production use.</remarks>
public static class HrfTestClientIntegration
{

   public static async Task RunHrfClientIntegrationTestAsync()
   {
      using var transport = new InMemoryTransport();
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