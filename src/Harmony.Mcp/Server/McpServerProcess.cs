using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Harmony.SemanticKernel.Core;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Server;


public class McpServerProcess
{

   /// <summary>
   /// Spawns a new MCP server process using the same executable as the current process.
   /// </summary>
   /// <param name="projectPath"></param>
   /// <param name="env"></param>
   /// <returns></returns>
   /// <exception cref="InvalidOperationException"></exception>
   public static Process SpawnServerForStdioSameExe(
      string[] serverArgs, IDictionary<string, string?>? env = null)
   {
      // Re-run the same compiled executable (framework-dependent or self-contained)
      var exe = Environment.ProcessPath ??
         throw new InvalidOperationException("Cannot resolve current executable path.");

      var psi = new ProcessStartInfo
      {
         FileName = exe,
         UseShellExecute = false,
         RedirectStandardInput = true,   // child stdin = our write
         RedirectStandardOutput = true,   // child stdout = our read
         RedirectStandardError = true,
         CreateNoWindow = true
      };

      // Example: ["server-stdio"]
      foreach (var a in serverArgs) 
         psi.ArgumentList.Add(a);

      if (env != null)
      {
         foreach (var kv in env)
         {
            if (!string.IsNullOrEmpty(kv.Value))
               psi.Environment[kv.Key] = kv.Value!;
         }
      }

      var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
      proc.ErrorDataReceived += (_, e) => 
      { 
         if (!string.IsNullOrEmpty(e.Data)) 
            KernelIO.Error.WriteLine($"[server] {e.Data}"); 
      };

      if (!proc.Start()) 
         throw new InvalidOperationException("Failed to start MCP server process.");
      proc.BeginErrorReadLine();

      // Optional: fail fast if it died immediately
      Thread.Sleep(250);
      if (proc.HasExited) 
         throw new InvalidOperationException($"Server exited early. ExitCode={proc.ExitCode}");

      return proc;
   }

}

