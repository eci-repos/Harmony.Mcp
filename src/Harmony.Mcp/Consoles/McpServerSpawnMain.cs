using Harmony.SemanticKernel.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Consoles;


public class McpServerSpawnMain
{

   public static void Main(string[] args)
   {
      string command = "dotnet";
      string _args = args.Length > 0 ? string.Join(' ', args) : "run --project ../McpSkServer";

      SpawnServer(command, _args, new Dictionary<string, string?>());
   }

   private static Process SpawnServer(string command, string args, Dictionary<string, string?> env)
   {
      var psi = new ProcessStartInfo(command, args)
      {
         RedirectStandardInput = true,
         RedirectStandardOutput = true,
         RedirectStandardError = true,
         UseShellExecute = false,
         CreateNoWindow = true
      };

      foreach (var kv in env)
      {
         if (!string.IsNullOrEmpty(kv.Value))
            psi.Environment[kv.Key] = kv.Value!;
      }

      var p = new Process { 
         StartInfo = psi, 
         EnableRaisingEvents = true 
      };

      p.ErrorDataReceived += (_, e) => { 
         if (!string.IsNullOrEmpty(e.Data)) 
            KernelIO.Error.WriteLine($"[server stderr] {e.Data}"); 
      };

      p.Start();
      p.BeginErrorReadLine();

      return p;
   }

}
