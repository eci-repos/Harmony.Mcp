using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// -------------------------------------------------------------------------------------------------
namespace Harmony.Mcp.Models;

public sealed class McpResultLog : McpError
{

   public DateTime LogDateTime { get; set; } = DateTime.UtcNow;
   public bool Success { get; set; } = false;

   public List<McpResultLog> Log { get; set; }

   public McpResultLog(List<McpResultLog>? log = null)
   {
      Log = log ?? new List<McpResultLog>(); 
   }

   public McpResultLog LogEntry(McpResultLog log, bool resetLog = false)
   {
      McpResultLog l = new McpResultLog(resetLog ? new List<McpResultLog>() : Log);
      l.Success = Success;
      l.Code = log.Code;
      l.Message = log.Message;
      l.Data = log.Data;
      Log.Add(l);
      return l;
   }

   public McpResultLog Failed(int code = 1, string? message = null)
   {
      Suceeded(code, null, null);
      LogEntry(this);
      return this;
   }

   public McpResultLog Succeeded()
   {
      Code = 0;
      LogEntry(this);
      return this;
   }

   public McpResultLog Succeeded(int code, string message)
   {
      Code = code;
      Message = message;
      LogEntry(this);
      return this;
   }

   public static McpResultLog Suceeded(int code, string? message, object? data)
   {
      McpResultLog log = new McpResultLog();
      log.Code = code;
      log.Message = message;
      log.Data = data;
      log.Success = log.Code == 0;
      log.Log.Add(log);
      return log;
   }

   public static McpResultLog GetLog(
      int code = 0, string? message = null, object? data = null)
   {
      McpResultLog log = new McpResultLog();
      log.Code = code;
      log.Message = message;
      log.Data = data;
      log.Success = log.Code == 0;
      log.Log.Add(log);
      return log;
   }

}
