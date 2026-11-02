using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using StockManufactura.Application.Interfaces;
using StockManufactura.Web.Infrastructure;
using Xunit;

namespace StockManufactura.Web.Tests;

/// <summary>
/// Operaciones sobre el archivo de la base: deben actuar sobre la base configurada en la conexión, nunca sobre la ruta
/// por defecto (en el hosting y en las pruebas la base vive en otra carpeta).
/// </summary>
public class BaseDeDatosTests
{
    [Fact]
    public async Task CopiaDeSeguridad_SeTomaDeLaBaseConfigurada()
    {
        using var sitio = new SitioWebFactory();
        await sitio.CrearUsuarioAsync("copias", "Copias123", "USUARIOS_ADMIN");
        var destino = Path.Combine(Path.GetTempPath(), $"copia-{Guid.NewGuid():N}.db");

        try
        {
            await sitio.Services.GetRequiredService<IDatabaseSnapshotService>().ExportAsync(destino);

            using var copia = new SqliteConnection($"Data Source={destino};Pooling=False");
            copia.Open();
            using var consulta = copia.CreateCommand();
            consulta.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE Nombre = 'copias'";
            Assert.Equal(1L, (long)consulta.ExecuteScalar()!);
        }
        finally
        {
            File.Delete(destino);
        }
    }

    [Fact]
    public async Task Restablecer_DejaElSistemaComoRecienInstaladoYRegistraLaOperacion()
    {
        using var sitio = new SitioWebFactory();
        await sitio.CrearUsuarioAsync("temporal", "Temporal123", "STOCK_VER");

        sitio.Services.GetRequiredService<DatabaseResetService>().Restablecer("admin@test.com");

        using var scope = sitio.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var usuarios = await unitOfWork.Usuarios.ListAsync();
        var bitacora = await unitOfWork.AuditLogs.ListAsync();
        Assert.Equal(new[] { "Admin" }, usuarios.Select(u => u.Nombre));
        Assert.DoesNotContain(usuarios, u => u.Nombre == "temporal");
        Assert.True(usuarios.Single().RequiereCambioPassword);
        Assert.Contains(bitacora, a => a.Accion == "ResetearBaseDatos" && a.Usuario == "admin@test.com");
    }

    [Fact]
    public void Restablecer_ExigeLaFraseDeConfirmacion()
    {
        Assert.Equal("BORRAR TODO", DatabaseResetService.FraseConfirmacion);
    }
}
