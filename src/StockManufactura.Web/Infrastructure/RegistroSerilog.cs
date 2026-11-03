using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Algunos servicios de la capa de aplicación escriben con el registrador estático de Serilog. Sin configurarlo, esos
    /// mensajes se descartan; aquí se los reenvía al registro de ASP.NET Core, que los muestra en la consola del servidor
    /// junto con el resto.
    /// </summary>
    public static class RegistroSerilog
    {
        public static void Configurar(ILoggerFactory fabrica)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Sink(new Reenvio(fabrica.CreateLogger("StockManufactura.Application")))
                .CreateLogger();
        }

        private sealed class Reenvio(Microsoft.Extensions.Logging.ILogger destino) : ILogEventSink
        {
            public void Emit(LogEvent evento)
            {
                var nivel = evento.Level switch
                {
                    LogEventLevel.Fatal => LogLevel.Critical,
                    LogEventLevel.Error => LogLevel.Error,
                    LogEventLevel.Warning => LogLevel.Warning,
                    LogEventLevel.Information => LogLevel.Information,
                    LogEventLevel.Debug => LogLevel.Debug,
                    _ => LogLevel.Trace
                };
                destino.Log(nivel, evento.Exception, "{Mensaje}", evento.RenderMessage());
            }
        }
    }
}
