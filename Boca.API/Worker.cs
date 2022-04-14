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
        public Worker(ILogger<BocaService> logger, IHostEnvironment environment, IConfiguration config, IOptions<Settings> options)
        {
            _logger = logger;
            _cfg = config;
            _settings = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connStr = _cfg.GetValue<string>("ConnectionStrings:BocaDBConnectionString");
            if (connStr == null) return;
            var repo = new BocaRepository(connStr);

            var service = new BocaService(repo, _logger, _settings);
           
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Worker running at: {time}", DateTimeOffset.Now);
                 await service.UploadInputFileToDatabase();
                 await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
