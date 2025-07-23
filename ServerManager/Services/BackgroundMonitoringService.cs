using ServerManager.Services;

namespace ServerManager.Services
{
    public class BackgroundMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundMonitoringService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Проверка каждые 5 минут

        public BackgroundMonitoringService(IServiceProvider serviceProvider, ILogger<BackgroundMonitoringService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background monitoring service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                                    using var scope = _serviceProvider.CreateScope();
                var serverService = scope.ServiceProvider.GetRequiredService<WindowsServerService>();
                    
                    _logger.LogInformation("Starting automated server status check");
                    await serverService.CheckAllServersStatusAsync();
                    _logger.LogInformation("Automated server status check completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during automated server status check");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Background monitoring service stopped");
        }
    }
}