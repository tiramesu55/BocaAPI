namespace BocaAPI.Interfaces
{
    public interface ILoggerService
    {
        void LogInfo(int rowNum, string message);
        void LogException(Exception ex);
    }
}
