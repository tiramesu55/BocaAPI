using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Repository;
using BocaAPI.Services;
using Microsoft.Extensions.Options;

namespace BocaAPI
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<BocaService> _logger;

        private readonly IConfiguration _cfg;

        private readonly IOptions<Settings> _settings;

        private readonly IOptions<EmailConfig> _emailConfig;

        public Worker(ILogger<BocaService> logger, IHostEnvironment environment, IConfiguration config, IOptions<Settings> options, IOptions<EmailConfig> emc)
        {
            _logger = logger;
            _cfg = config;
            _settings = options;
            _emailConfig= emc;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connStr = _cfg.GetValue<string>("ConnectionStrings:BocaDBConnectionString");
            if (connStr == null) return;
            var repo = new BocaRepository(connStr);
            var email = new Email(_logger, _emailConfig);
            var service = new BocaService(repo, _logger, _settings,email);
           
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Worker running at: {time}", DateTimeOffset.Now);
                 await service.UploadInputFileToDatabase();
                 await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                //archive if time after 11PM and before midnight
                var currentTime = DateTimeOffset.Now.TimeOfDay.Hours;
                if (currentTime > 23)
                    await service.Archive();
            }
            _logger.LogCritical("_Boca Service Worker Stoppes Unexpectingly.  Please restart service");
        }
    }
}
