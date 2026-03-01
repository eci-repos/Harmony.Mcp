using Harmony.Tooling.Models;
using Harmony.Tooling.Preflight;
using Harmony.Tooling.Scripts;
using Microsoft.AspNetCore.Mvc;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Controllers;

[ApiController]
[Route("preflight")]
public sealed class PreflightController : ControllerBase
{
   private readonly IScriptStore _scriptStore;           // format-agnostic
   private readonly IToolDependencyExtractor _extractor; // format-specific impl injected
   private readonly IToolPreflightAnalyzer _analyzer;    // abstraction

   public PreflightController(
       IScriptStore scriptStore,
       IToolDependencyExtractor extractor,
       IToolPreflightAnalyzer analyzer)
   {
      _scriptStore = scriptStore ?? throw new ArgumentNullException(nameof(scriptStore));
      _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
      _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
   }

   public sealed class AnalyzeRequest
   {
      public required string ScriptId { get; init; }
      /// <summary>"strict" | "replay-allowed"; defaults to "strict"</summary>
      public string? Mode { get; init; }
   }

   [HttpPost("analyze")]
   public async Task<ActionResult<PreflightReport>> Analyze([FromBody] AnalyzeRequest req, CancellationToken ct)
   {
      if (string.IsNullOrWhiteSpace(req.ScriptId))
         return BadRequest("ScriptId must be provided.");

      // 1) Load the script as an opaque object (no Harmony.Format dependency here).
      var script = await _scriptStore.GetAsync<object>(req.ScriptId, ct).ConfigureAwait(false);
      if (script is null)
         return NotFound(new { error = "script_not_found", scriptId = req.ScriptId });

      // 2) Extract recipients using the format-specific extractor (provided via DI).
      var recipients = await _extractor.ExtractRecipientsAsync(script, ct).ConfigureAwait(false);

      // 3) Analyze against current tool availability (strict by default).
      var mode = string.IsNullOrWhiteSpace(req.Mode)
          ? PreflightReport.DefaultPreflightMode   // "strict"
          : req.Mode!;

      var report = await _analyzer.AnalyzeAsync(recipients, mode, ct).ConfigureAwait(false);

      return Ok(report);
   }
}