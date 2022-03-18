using BocaAPI.Interfaces;
using Serilog;
using Serilog.Context;

namespace BocaAPI.Services
{
    public class LoggerService : ILoggerService
    {
        public LoggerService()
        {

        }

        public void LogInfo(int rowNum, string message)
        {
            Log.ForContext("RowNum", rowNum).Information("{Message}", message);

            Log.CloseAndFlush();
        }
    }
}
