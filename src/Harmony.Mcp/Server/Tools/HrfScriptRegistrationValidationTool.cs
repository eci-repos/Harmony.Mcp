using Harmony.Mcp.Server.Protocols;
using Harmony.SemanticKernel.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server.Tools;

/// <summary>
/// Represents the input data required to register or validate an HRF (Human-Readable Format) 
/// script, including script content, identifier, and registration options.
/// </summary>
/// <remarks>This type is used to encapsulate all information necessary for HRF script registration 
/// workflows, such as script validation, content type specification, and overwrite behavior. It is
/// intended for internal use within the registration and tool creation process.</remarks>
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

   public RequestResult Validate()
   {
      // Conservative content-type/body validation (uses Protocols.HrfValidator)
      if (!HrfValidator.ValidateContentTypeHeader(ContentType, Script))
      {
         return RequestResult.Fail("HRF content-type/body validation failed.");
      }
      return RequestResult.Okey(null);
   }

   private void CreateToolsForResponses(
      ToolRegistry registry, Dictionary<string,string> scripts)
   {
      // Parse script again to create tools for each response
      using var scriptDoc = JsonDocument.Parse(Script);
      var rootEl = scriptDoc.RootElement;
      var responses = rootEl.GetProperty("responses");
      foreach (var r in responses.EnumerateArray())
      {
         var respName = r.GetProperty("name").GetString()!;
         var contentElement = r.GetProperty("content");
         var contentRaw = contentElement.GetRawText(); // capture raw text for closure
         var toolName = $"{Id}.{respName}";
         // If tool already exists and overwrite==false, skip or fail — here we
         // overwrite the tool registration.  Create a minimal input schema
         // (no inputs required).
         var toolSchema = JsonDocument.Parse(@"{ ""type"": ""object"" }");
         registry.AddTool(
            new McpTool(
               name: toolName,
               description: $"HRF response tool ({Id}/{respName})",
               inputSchema: toolSchema,
               handler: async (p, ctoken) =>
               {
                  // Return content as parsed JSON element or string
                  // depending on original kind
                  try
                  {
                     using var elDoc = JsonDocument.Parse(contentRaw);
                     var elem = elDoc.RootElement;
                     object? data;
                     if (elem.ValueKind == JsonValueKind.String)
                        data = elem.GetString();
                     else
                        data = elem; // return JsonElement for structured content
                     return RequestResult.Okey(
                        new { id = Id, name = respName, content = data });
                  }
                  catch (JsonException)
                  {
                     // Fallback: return raw string if parsing fails
                     return RequestResult.Okey(
                        new { id = Id, name = respName, content = contentRaw });
                  }
               }
            )
         );
      }
   }

   /// <summary>
   /// Public method to parse and register HRF script.
   /// </summary>
   /// <param name="registry">Tool Registry</param>
   /// <param name="scripts">register dictionary (name / value)</param>
   /// <returns>RequestResult instance with reults info</returns>
   public RequestResult ParseAndRegister(ToolRegistry registry, Dictionary<string,string> scripts)
   {
      JsonDocument? scriptDoc = null;
      try
      {
         scriptDoc = JsonDocument.Parse(Script);
         var rootEl = scriptDoc.RootElement;

         // Basic structural validation: require 'responses' array
         if (!rootEl.TryGetProperty("responses", out var responses) ||
             responses.ValueKind != JsonValueKind.Array)
         {
            return RequestResult.Fail(
               "HRF script must contain a top-level 'responses' array.");
         }

         // Quick uniqueness check for response names
         var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         foreach (var r in responses.EnumerateArray())
         {
            if (!r.TryGetProperty("name", out var nm) ||
                nm.ValueKind != JsonValueKind.String)
               return RequestResult.Fail(
                  "Each response must include a string 'name' property.");
            var respName = nm.GetString()!;
            if (!names.Add(respName))
               return RequestResult.Fail($"Duplicate response name: {respName}");
         }

         if (ValidateOnly)
         {
            return RequestResult.Okey(
               new 
               { 
                  Id, 
                  validated = true,
                  responsesCount = names.Count
               });
         }

         // Registration: store script and create per-response tools
         if (scripts.ContainsKey(Id) && !Overwrite)
            return RequestResult.Fail(
               $"Script with id '{Id}' already exists. Use overwrite=true to replace.");

         scripts[Id] = Script;

         // Create tools for each response
         CreateToolsForResponses(registry, scripts);

         return RequestResult.Okey(
            new { id = Id, validated = true, responseCount = names.Count });
      }
      catch (JsonException ex)
      {
         return RequestResult.Fail("Script is not valid JSON: " + ex.Message);
      }
      finally
      {
         scriptDoc?.Dispose();
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
            description: "Validate and (optionally) register an HRF script. Creates per-response "
                       + "tools named '{id}.{responseName}'.",
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
               RequestResult response = input.ParseAndRegister(registry, scripts);
            }
         );

   }
}
