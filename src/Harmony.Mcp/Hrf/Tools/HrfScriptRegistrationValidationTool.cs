using Harmony.Mcp.Hrf.Protocols;
using Harmony.SemanticKernel.Core;
using Harmony.Mcp.Server;

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Tools;

internal class HrfScriptRegistrationInput
{
   public string Id { get; set; } = string.Empty;
   public string Script { get; set; } = string.Empty;
   public string? ContentType { get; set; }
   public bool ValidateOnly { get; set; } = false;
   public bool Overwrite { get; set; } = false;

   public static HrfScriptRegistrationInput FromJsonElement(JsonElement el)
   {
      var input = new HrfScriptRegistrationInput
      {
         Id = el.GetProperty("id").GetString()!,
         Script = el.GetProperty("script").GetString()!
      };
      if (el.TryGetProperty("contentType", out var ctEl))
         input.ContentType = ctEl.GetString();
      if (el.TryGetProperty("validateOnly", out var vEl))
         input.ValidateOnly = vEl.GetBoolean();
      if (el.TryGetProperty("overwrite", out var oEl))
         input.Overwrite = oEl.GetBoolean();
      return input;
   }

   /// <summary>
   /// Validate the HRF script content and content-type header. This is a conservative check to 
   /// catch common issues
   /// </summary>
   /// <returns>RequestResult instance is returned</returns>
   public RequestResult Validate()
   {
      // Conservative content-type/body validation (uses Protocols.HrfValidator)
      if (!HrfValidator.ValidateContentTypeHeader(ContentType, Script))
      {
         return RequestResult.Fail("HRF content-type/body validation failed.");
      }
      return RequestResult.Okey(null);
   }

   /// <summary>
   /// Simple registration of an HRF script. Stores the raw script text in the supplied dictionary 
   /// using the provided Id as the key.
   /// </summary>
   /// <param name="registry">ToolRegistry instance, not used in current implementation but may be
   /// useful for future extensions</param>
   /// <param name="scripts">HRF script to validate and register</param>
   /// <returns>RequestResult instance is returned</returns>
   public RequestResult RegisterScript(ToolRegistry registry, Dictionary<string,string> scripts)
   {
      try
      {
         if (ValidateOnly)
         {
            return RequestResult.Okey(new { Id, validated = true });
         }

         if (scripts.ContainsKey(Id) && !Overwrite)
            return RequestResult.Fail(
               $"Script with id '{Id}' already exists. Use overwrite=true to replace.");

         scripts[Id] = Script;

         return RequestResult.Okey(new { id = Id, registered = true });
      }
      catch (Exception ex)
      {
         return RequestResult.Fail("Registration failed: " + ex.Message);
      }
   }

}

// -------------------------------------------------------------------------------------------------
internal class HrfScriptRegistrationTool
{

   public static McpTool GetTool(
      ToolRegistry registry, Dictionary<string, string> scripts)
   {
      // Tool to validate HRF script registration
      return
         new McpTool(
            name: "hrf.register",
            description: "Validate and (optionally) register an HRF script for later retrieval.",
            inputSchema: JsonDocument.Parse(@"{
               ""type"": ""object"",
               ""properties"": {
                  ""id"": { ""type"": ""string"" },
                  ""script"": { ""type"": ""string"" },
                  ""contentType"": { ""type"": ""string"" },
                  ""validateOnly"": { ""type"": ""boolean"", ""default"": false },
                  ""overwrite"": { ""type"": ""boolean"", ""default"": false }
               },
               ""required"": [""id"", ""script""]
            }"),
            handler: async (payload, ct) =>
            {
               KernelIO.Log.WriteLine("HRF register tool called.");

               var input = HrfScriptRegistrationInput.FromJsonElement(payload.RootElement);

               input.Validate();
               RequestResult response = input.RegisterScript(registry, scripts);
               return response;
            }
         );

   }
}
