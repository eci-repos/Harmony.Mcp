using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Harmony.Mcp.Models;
using Harmony.Mcp.Transports;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Client;

public interface IClientMain
{
    Task<McpResultLog> Main(string[]? args, IMcpTransport? transport = null);
}
