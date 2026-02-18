using Grpc.Core;
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
   /// Sample HRF script demonstrating a messages-based harmony script with multiple steps, 
   /// including input extraction and a tool call. This script is serialized to JSON format and 
   /// can be used for testing HRF script registration and execution.
   /// </summary>
   public static string SampleDefaultHrfScript = JsonSerializer.Serialize(new
   {
      hrfVersion = "1.0.0",
      messages = new object[] {
         new {
            role = "system",
            channel = "analysis",
            contentType = "text",
            content = "You are M365 Copilot. Follow HRF."
         },
         new {
            role = "user",
            channel = "analysis",
            contentType = "text",
            content = "Find two nearby coffee shops and summarize them."
         },
         new {
            role = "assistant",
            channel = "commentary",
            recipient = "orchestrator.plan",
            contentType = "harmony-script",
            termination = "end",
            content = new {
               steps = new object[] {
                  new {
                     type = "extract-input",
                     output = new { userQuery = "$input.text" }
                  },
                  new {
                     type = "tool-call",
                     recipient = "maps.search",
                     channel = "commentary",
                     args = new { query = "$vars.userQuery", limit = 2 },
                     save_as = "places"
                  }
               }
            }
         },
         new {
            role = "assistant",
            channel = "final",
            contentType = "text",
            termination = "return",
            content = "Here are two nearby coffee shops with brief summaries:\n\n1) "
               + "Shop A — cozy atmosphere.\n2) Shop B — fast Wi‑Fi.\n\nWant directions?"
         }
      }
   }, McpJson.Options);

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
   /// <param name="scripts">Optional list of HRF script contents to use for testing. If null, 
   /// a default sample script will be used.</param>
   /// <returns>A task that represents the asynchronous operation.</returns>
   public static async Task RunExampleAsync(McpClient client, List<string>? scripts = null)
   {
      var hrf = new HrfClient(client);

      // if no scripts provided, use the sample default script for testing
      List<string> scriptList = scripts ?? [SampleDefaultHrfScript];

      // [TODO:] Implement code to loop through multiple scripts.

      // HRF script following expected schema (messages-based harmony script example)
      string sampleScript = SampleDefaultHrfScript;

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

      // Retrieve the registered HRF script and log it (tests the new hrf.get tool)
      KernelIO.Log.WriteLine("Retrieving registered HRF script...");
      var registeredScript = await hrf.GetRegisteredScriptAsync(registerArgs.id);
      if (registeredScript == null)
      {
         KernelIO.Log.WriteLine($"Registered script '{registerArgs.id}' not found.");
      }
      else
      {
         KernelIO.Log.WriteLine("Registered script content:");
         KernelIO.Log.WriteLine(registeredScript);
      }

      // Now list registered scripts to test the new hrf.list tool
      Console.WriteLine("Listing registered HRF scripts...");
      var list = await hrf.ListRegisteredScriptsAsync();
      if (list == null)
      {
         Console.WriteLine("Failed to list registered scripts.");
      }
      else
      {
         Console.WriteLine("Registered script ids:");
         foreach (var id in list) Console.WriteLine(" - " + id);
      }

      // Test deletion of the registered script using hrf.delete
      Console.WriteLine($"Deleting registered script '{registerArgs.id}'...");
      var deleted = await hrf.DeleteRegisteredScriptAsync(registerArgs.id);
      if (deleted == true)
      {
         Console.WriteLine($"Script '{registerArgs.id}' deleted successfully.");
      }
      else if (deleted == false)
      {
         Console.WriteLine($"Script '{registerArgs.id}' not found to delete.");
      }
      else
      {
         Console.WriteLine($"Failed to delete script '{registerArgs.id}'.");
      }

      // Verify deletion by listing scripts again
      Console.WriteLine("Listing registered HRF scripts after deletion...");
      var listAfter = await hrf.ListRegisteredScriptsAsync();
      if (listAfter == null)
      {
         Console.WriteLine("Failed to list registered scripts after deletion.");
      }
      else
      {
         Console.WriteLine("Registered script ids after deletion:");
         foreach (var id in listAfter) Console.WriteLine(" - " + id);
      }
   }

}
