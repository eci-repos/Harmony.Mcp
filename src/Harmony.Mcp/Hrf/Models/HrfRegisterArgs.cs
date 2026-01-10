using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Hrf.Models;

/// <summary>
/// Arguments for HRF registration calls.
/// </summary>
public sealed class HrfRegisterArgs
{
   public string id { get; set; } = string.Empty;
   public string script { get; set; } = string.Empty;
   public string? contentType { get; set; }
   public bool validateOnly { get; set; } = false;
   public bool overwrite { get; set; } = false;
}

