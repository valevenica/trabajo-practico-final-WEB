using Microsoft.Extensions.Logging;
using StockManufactura.Web.Infrastructure;
using Xunit;

namespace StockManufactura.Web.Tests;

/// <summary>
/// Los servicios que escriben con el registrador estático de Serilog deben aparecer en el registro de ASP.NET Core;
/// antes, sin configurarlo, sus errores se perdían.
/// </summary>
public class RegistroSerilogTests
{
    [Fact]
    public void ErrorEscritoConSerilog_LlegaAlRegistroDelServidor()
    {
        var capturados = new List<(LogLevel Nivel, string Mensaje, Exception? Error)>();
        using var fabrica = LoggerFactory.Create(b => b.AddProvider(new Proveedor(capturados)));

        RegistroSerilog.Configurar(fabrica);
        var falla = new InvalidOperationException("sin conexión");
        Serilog.Log.Error(falla, "No se pudo enviar el codigo a {Email}", "produccion@demo.local");

        var (nivel, mensaje, error) = Assert.Single(capturados);
        Assert.Equal(LogLevel.Error, nivel);
        Assert.Contains("produccion@demo.local", mensaje);
        Assert.Same(falla, error);
    }

    private sealed class Proveedor(List<(LogLevel, string, Exception?)> destino) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoria) => new Registrador(destino);
        public void Dispose() { }
    }

    private sealed class Registrador(List<(LogLevel, string, Exception?)> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => true;

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? error, Func<TState, Exception?, string> formato)
            => destino.Add((nivel, formato(estado, error), error));
    }
}
