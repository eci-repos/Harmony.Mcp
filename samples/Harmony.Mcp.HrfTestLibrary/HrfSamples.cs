using Harmony.Mcp.Client;
using Harmony.Mcp.Hrf;
using Harmony.Mcp.Hrf.Models;
using Harmony.Mcp.Models;
using Harmony.SemanticKernel.Core;
using System.Text.Json;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.HrfTestLibrary;

/// <summary>
/// Example/test usage for HRF client helpers. Call from an initialized McpClient instance.
/// </summary>
public static class HrfSamples
{

   /// <summary>
   /// Demonstrates the process of validating, registering, and invoking responses from an HRF 
   /// script using the specified MCP client.
   /// </summary>
   /// <remarks>This example method shows how to validate an HRF script before registration, 
   /// register the script with overwrite enabled, and call specific response tools 
   /// ('summary' and 'details') using the HRF client. Output is written to the log for each step.
   /// This method is intended for demonstration or testing purposes.</remarks>
   /// <param name="client">The MCP client instance used to communicate with the HRF service. 
   /// Cannot be null.</param>
   /// <returns>A task that represents the asynchronous operation.</returns>
   public static async Task RunExampleAsync(McpClient client)
   {
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

      var registerArgs = new HrfRegisterArgs
      {
         id = "sample_hfr",
         script = sampleScript,
         contentType = "application/harmony+json",
         validateOnly = true // first validate
      };

      KernelIO.Log.WriteLine("Validating HRF script...");
      var validation = await hrf.RegisterScriptAsync(registerArgs);
      KernelIO.Log.WriteLine($"Validated: id={validation.id} "
         + "validated={validation.validated} responses={validation.responseCount}");

      // Now register for real (overwrite if present)
      registerArgs.validateOnly = false;
      registerArgs.overwrite = true;

      KernelIO.Log.WriteLine("Registering HRF script...");
      var registration = await hrf.RegisterScriptAsync(registerArgs);
      KernelIO.Log.WriteLine($"Registered: id={registration.id} "
         + "registered={registration.registered} responses={registration.responseCount}");

      // Call the 'summary' response tool
      KernelIO.Log.WriteLine("Calling response tool: summary");
      var summary = await hrf.CallResponseAsync(registerArgs.id, "summary");
      if (summary.StringContent != null)
      {
         KernelIO.Log.WriteLine($"summary => {summary.StringContent}");
      }
      else if (summary.RawContent.HasValue)
      {
         KernelIO.Log.WriteLine($"summary (json) => {summary.RawContent.Value.GetRawText()}");
      }

      // Call the 'details' response tool
      KernelIO.Log.WriteLine("Calling response tool: details");
      var details = await hrf.CallResponseAsync(registerArgs.id, "details");
      if (details.StringContent != null)
      {
         KernelIO.Log.WriteLine($"details => {details.StringContent}");
      }
      else if (details.RawContent.HasValue)
      {
         KernelIO.Log.WriteLine($"details (json) => {details.RawContent.Value.GetRawText()}");
      }
   }

}
