using BocaAPI.Interfaces;

namespace BocaAPI.Services
{
    public abstract class ServiceBase
    {
        protected readonly ILoggerService _logger;

        public ServiceBase(ILoggerService logger)
        {
            _logger = logger;
        }


        protected virtual async Task ExecuteOperationAsync(Func<Task> func)
        {
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
            }
        }

        protected virtual async Task<T?> ExecuteOperationAsync<T>(Func<Task<T>> func) where T : class
        {
            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                _logger.LogException(ex);
                return null;
            }
        }


    }
}
