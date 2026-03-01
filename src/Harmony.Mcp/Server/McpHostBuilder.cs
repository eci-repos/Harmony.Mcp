// Program.cs (agnostic)

using Harmony.Format.Execution.Preflight;      // HRF extractor
using Harmony.Format.SemanticKernel;           // Optional SK registry
using Harmony.Format.SemanticKernel.Tooling;
using Harmony.Tooling.Contracts;
using Harmony.Tooling.Discovery;
using Harmony.Tooling.Preflight;
using Harmony.Tooling.Scripts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server;

/// <summary>
/// MCP Host Builder that can run in multiple modes (e.g. console/worker or web API) based on
/// configuration. 
/// </summary>
public class McpHostBuilder
{

   /// <summary>
   /// Main entry point for the MCP Host application. Initializes the host builder, configures 
   /// services, and runs the application in either console/worker mode or web API mode based on 
   /// configuration (e.g. environment variable "Mode"). In web API mode, it sets up controllers 
   /// and endpoints; in console mode, it resolves necessary services and runs background tasks 
   /// as needed. 
   /// </summary>
   /// <param name="args">arguments </param>
   /// <returns></returns>
   public static async Task Main(string[] args)
   {
      var builder = Host.CreateApplicationBuilder(args); // .NET 8
      ConfigureServices(builder.Services);

      // Decide mode (env var, command-line arg, etc.)
      var mode = builder.Configuration["Mode"] ?? "Console";

      // In a real app, you might want to support running both modes simultaneously
      // (e.g. web API + background worker)
      if (string.Equals(mode, "Web", StringComparison.OrdinalIgnoreCase))
      {
         // Add Web API only if you run as a web host
         builder.Services.AddControllers();
      }

      using var host = builder.Build();

      // Run in different modes based on configuration
      if (string.Equals(mode, "Web", StringComparison.OrdinalIgnoreCase))
      {
         // Build a WebApplication on top of the existing Services (minimal glue)
         var appBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
         {
            // Reuse host configuration/environment as needed
            Args = args
         });

         // IMPORTANT: reuse existing ServiceProvider where possible (or re-call ConfigureServices)
         // For simplicity, we rebuild here using the same method:
         ConfigureServices(appBuilder.Services);
         appBuilder.Services.AddControllers();

         var app = appBuilder.Build();
         app.MapControllers();
         app.MapGet("/", () => "Harmony MCP (web) is up.");
         await app.RunAsync();
      }
      else
      {
         // Console/worker logic: resolve and run your services
         var sp = host.Services;

         var store = sp.GetRequiredService<IScriptStore>();
         var extractor = sp.GetRequiredService<IToolDependencyExtractor>();
         var analyzer = sp.GetRequiredService<IToolPreflightAnalyzer>();

         // ... do console work here ...
         Console.WriteLine("Harmony MCP (console) is up.");
         await host.RunAsync(); // keeps background services alive if any
      }
   }

   /// <summary>
   /// Configures the services for the MCP Host application. Registers necessary services for both 
   /// console/worker and web API modes, including script store, tool dependency extractor, 
   /// preflight analyzer, and tool registry/availability. The tool registry is backed by a kernel
   /// instance, which can be shared if you use SK as the host or replaced with your own 
   /// implementation. This method is called in both the console/worker and web API modes to 
   /// ensure that the necessary services are available regardless of how the application is run. 
   /// </summary>
   /// <param name="services"></param>
   public static void ConfigureServices(IServiceCollection services)
   {
      // Agnostic DI graph (no web types needed)
      services.AddSingleton<IScriptStore, InMemoryScriptStore>();
      services.AddSingleton<IToolPreflightAnalyzer, ToolPreflightAnalyzer>();
      services.AddSingleton<IToolDependencyExtractor, HarmonyToolDependencyExtractor>();

      // Tooling availability (backed by a registry you provide)
      services.AddSingleton<IToolRegistry>(sp =>
      {
         // If you use SK as host; otherwise, return your own registry
         var kernel = Kernel.CreateBuilder().Build();
         return new KernelToolRegistry(kernel);
      });
      services.AddSingleton<IToolAvailability, RegistryToolAvailability>(sp =>
          new RegistryToolAvailability(sp.GetRequiredService<IToolRegistry>()));
   }

}
