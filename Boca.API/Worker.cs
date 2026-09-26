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
            //minutes between runs; fall back to 30 if missing or invalid so the loop never spins without delay
            var frequency = _cfg.GetValue<int>("Folders:Frequency");
            if (frequency <= 0) frequency = 30;
            if (connStr == null) return;
            var repo = new BocaRepository(connStr);
            var email = new Email(_logger, _emailConfig);
            var service = new BocaService(repo, _logger, _settings,email);
            var lastArchiveDate = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Worker running at: {time}", DateTimeOffset.Now);
                 await service.UploadInputFileToDatabase();
                 await Task.Delay(TimeSpan.FromMinutes(frequency), stoppingToken);
                //archive once a day, on the first cycle between 11PM and midnight
                var now = DateTime.Now;
                if (now.Hour >= 23 && lastArchiveDate != now.Date)
                {
                    lastArchiveDate = now.Date;
                    _logger.LogWarning("Archiving police_master records older than 1 year at: {time}", now);
                    await service.Archive();
                }
            }
            _logger.LogCritical("_Boca Service Worker Stoppes Unexpectingly.  Please restart service");
        }
    }
}
