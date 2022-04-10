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
        public void LogError(int rowNum, string message)
        {
            //Log.ForContext("RowNum", rowNum).Error("{Message}", message);

          //  Log.CloseAndFlush();
        }
        public void LogException(Exception ex)
        {
          //  Log.Error(ex.ToString());

         //   Log.CloseAndFlush();
        }
    }
}
