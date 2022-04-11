using BocaAPI.Interfaces;

namespace BocaAPI.Services
{
    public abstract class ServiceBase
    {
        protected readonly ILogger<ServiceBase> _logger;

        public ServiceBase(ILogger<ServiceBase> logger)
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
                _logger.LogError(ex.Message);
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
                _logger.LogError(ex.Message);
                return null;
            }
        }


    }
}
