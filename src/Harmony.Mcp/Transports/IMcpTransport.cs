using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Transports;

public interface IMcpTransport : IDisposable
{
   Task WriteAsync(string json, CancellationToken ct);
   Task<(bool ok, string body)> ReadAsync(CancellationToken ct);
}
