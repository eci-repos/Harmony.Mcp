// /Harmony.Mcp/Controllers/CapabilitiesController.cs  (illustrative)
using Harmony.Mcp.Server;
using Harmony.Tooling.Contracts;
using Harmony.Tooling.Models;
using Microsoft.AspNetCore.Mvc;

// -----------------------------------------------------------------------------
// This is a simple controller to list the capabilities of the MCP.
namespace Harmony.Mcp.Controllers;

[ApiController]
[Route("capabilities")]
public sealed class CapabilitiesController : ControllerBase
{
   private readonly IToolRegistry _registry;
   private readonly IDictionary<string, string> _providers;

   public CapabilitiesController(IToolRegistry registry)
   {
      _registry = registry;
      _providers = new Dictionary<string, string> {  // examples
         { "mail", "Graph" },
         { "files", "OneDrive" }, 
         { "memory", "Qdrant" }
        };
   }

   [HttpGet("list")]
   public ActionResult<Capabilities> List()
       => Ok(new Capabilities { Tools = _registry.List(), Providers = _providers });
}
