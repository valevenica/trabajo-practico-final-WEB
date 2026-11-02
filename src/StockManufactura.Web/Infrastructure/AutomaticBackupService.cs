using StockManufactura.Application.Interfaces;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Copia automática: cada cinco minutos pregunta al servicio de copias si corresponde
    /// generar la automática. El servicio decide según la configuración (activada o no) y el intervalo desde la última.
    /// </summary>
    public sealed class AutomaticBackupService : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutomaticBackupService> _logger;

        public AutomaticBackupService(IServiceScopeFactory scopeFactory, ILogger<AutomaticBackupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Intervalo);
            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var record = await scope.ServiceProvider.GetRequiredService<IBackupService>()
                        .RunAutomaticBackupIfDueAsync("sistema-web", stoppingToken);
                    if (record is not null)
                    {
                        _logger.LogInformation("Copia de seguridad automática generada en {Ruta}", record.RutaArchivo);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Falló la copia de seguridad automática");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
