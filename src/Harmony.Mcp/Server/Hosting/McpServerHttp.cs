using Harmony.Mcp.Models;
using Harmony.Mcp.Server.Protocols;
using Harmony.SemanticKernel.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server.Hosting;

internal class McpServerHttp
{

   // ASP.NET Core HTTPS/HTTP server
   public static async Task RunHttpAsync(
       McpServer server, string bind, JsonSerializerOptions jsonOptions)
   {
      var builder = WebApplication.CreateBuilder(new WebApplicationOptions
      {
         Args = new[] { "--urls", bind }
      });

      // Configure services
      builder.Services.AddSingleton(server);
      builder.Services.AddSingleton(jsonOptions);

      // Add CORS for web clients (optional)
      builder.Services.AddCors(options =>
      {
         options.AddPolicy("AllowAll", policy =>
         {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
         });
      });

      var app = builder.Build();

      // Enable CORS
      app.UseCors("AllowAll");

      // CRITICAL: Register HRF middleware BEFORE endpoint routing
      app.UseHrfMediaTypes();

      // Map MCP endpoint
      app.MapPost("/mcp", async (HttpContext context, McpServer mcpServer) =>
      {
         try
         {
            // Check if request came through HRF
            bool isHrf = context.Items.ContainsKey("Harmony.Format.IsHrf");

            if (isHrf)
            {
               var mediaType = context.Items["Harmony.Format.MediaType"];
               Console.WriteLine($"[HTTPS] HRF request received: {mediaType}");
            }

            // Read the normalized JSON body (HRF middleware already processed it)
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            var requestBody = await reader.ReadToEndAsync();

            var request = JsonSerializer.Deserialize<McpRpcRequest>(
                requestBody, jsonOptions);

            if (request == null)
            {
               context.Response.StatusCode = 400;
               await context.Response.WriteAsJsonAsync(new
               {
                  error = "Invalid JSON-RPC request"
               });
               return;
            }

            // Handle the MCP request
            var response = await HandleMcpRequestAsync(
                mcpServer, request, context.RequestAborted);

            // Set response content type (match HRF if applicable)
            if (isHrf)
            {
               var originalMediaType = context.Items["Harmony.Format.MediaType"] as string;
               context.Response.ContentType = originalMediaType ?? "application/json";
            }
            else
            {
               context.Response.ContentType = "application/json; charset=utf-8";
            }

            await context.Response.WriteAsJsonAsync(response, jsonOptions);
         }
         catch (Exception ex)
         {
            Console.WriteLine($"[HTTPS] Error: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
               error = ex.Message
            });
         }
      });

      // Health check endpoint
      app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

      // Server info endpoint
      app.MapGet("/", () => Results.Ok(new
      {
         name = "Harmony MCP Server",
         version = "0.2.0",
         protocols = new[] { "MCP", "HRF" },
         endpoint = "/mcp"
      }));

      Console.WriteLine($"[server] HTTPS listening on {bind}");
      Console.WriteLine($"[server] MCP endpoint: {bind}/mcp");
      Console.WriteLine($"[server] HRF middleware: ENABLED");

      await app.RunAsync();
   }

   // Helper method to handle MCP requests
   private static async Task<McpRpcResponse> HandleMcpRequestAsync(
       McpServer server, McpRpcRequest request, CancellationToken ct)
   {
      // Access the private HandleAsync method via reflection
      // OR expose it as public/internal in McpServer.cs
      var method = typeof(McpServer).GetMethod(
          "HandleAsync",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

      if (method != null)
      {
         var task = method.Invoke(server, new object[] { request, ct }) as Task<McpRpcResponse>;
         return await task!;
      }

      return McpRpcResponse.RpcError(
          request.Id, -32603, "Internal server error");
   }

}
