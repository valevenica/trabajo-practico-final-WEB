#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockManufactura.Domain.Entities;
using StockManufactura.Infrastructure.Db;
using StockManufactura.Infrastructure.Repositories;
using Xunit;

namespace StockManufactura.UnitTests;

/// <summary>
/// Reproduce el uso de una pantalla con contexto de larga vida: alta, recarga del listado sin seguimiento y ediciones
/// sucesivas sobre las copias recargadas.
/// </summary>
public class RepositorySeguimientoTests
{
    [Fact]
    public async Task CrearYEditarDosVeces_EnElMismoContexto_GuardaLaUltimaEdicion()
    {
        using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        connection.Open();
        var options = new DbContextOptionsBuilder<StockManufacturaDbContext>().UseSqlite(connection).Options;
        using (var setup = new StockManufacturaDbContext(options))
        {
            setup.Database.Migrate();
        }

        using var context = new StockManufacturaDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        await unitOfWork.Clientes.AddAsync(new Cliente { Nombre = "Cliente nuevo" });
        await unitOfWork.SaveChangesAsync();

        foreach (var nombre in new[] { "Primera edición", "Segunda edición" })
        {
            var copia = (await unitOfWork.Clientes.ListAsync()).Single();
            copia.Nombre = nombre;
            unitOfWork.Clientes.Update(copia);
            await unitOfWork.SaveChangesAsync();
        }

        var aBorrar = (await unitOfWork.Clientes.ListAsync()).Single();
        Assert.Equal("Segunda edición", aBorrar.Nombre);
        unitOfWork.Clientes.Delete(aBorrar);
        await unitOfWork.SaveChangesAsync();

        using var verificacion = new StockManufacturaDbContext(options);
        Assert.Empty(await verificacion.Clientes.ToListAsync());
    }

    /// <summary>
    /// Pantalla de órdenes: se crea una orden con su alta en el historial, se recarga el listado sin seguimiento y, en el
    /// mismo contexto, se planifica la copia recargada. El cambio de estado tiene que quedar en el historial.
    /// </summary>
    [Fact]
    public async Task CrearYPlanificarUnaOrden_EnElMismoContexto_RegistraAmbosCambiosDeEstado()
    {
        using var connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        connection.Open();
        var options = new DbContextOptionsBuilder<StockManufacturaDbContext>().UseSqlite(connection).Options;
        using (var setup = new StockManufacturaDbContext(options))
        {
            setup.Database.Migrate();
        }

        using var context = new StockManufacturaDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var producto = new Producto { Codigo = "PRD-1", Nombre = "Vela", Activo = true };
        await unitOfWork.Productos.AddAsync(producto);
        var orden = new OrdenProduccion("OP-1", producto.Id, 2, string.Empty);
        await unitOfWork.OrdenesProduccion.AddAsync(orden);
        await unitOfWork.CambiosEstadoOrden.AddAsync(new OrdenProduccionCambioEstado(orden.Id, null, orden.Estado, "admin"));
        await unitOfWork.SaveChangesAsync();

        var copia = (await unitOfWork.OrdenesProduccion.ListByCreatedDescAsync()).Single();
        var anterior = copia.Estado;
        copia.Planificar();
        await unitOfWork.CambiosEstadoOrden.AddAsync(new OrdenProduccionCambioEstado(copia.Id, anterior, copia.Estado, "admin"));
        unitOfWork.OrdenesProduccion.Update(copia);
        await unitOfWork.SaveChangesAsync();

        using var verificacion = new StockManufacturaDbContext(options);
        var historial = await verificacion.OrdenesProduccionCambiosEstado.AsNoTracking().ToListAsync();
        Assert.Equal(2, historial.Count);
        Assert.Contains(historial, x => x.EstadoAnterior == EstadoOrdenProduccion.Borrador && x.EstadoNuevo == EstadoOrdenProduccion.Planificada);
        Assert.Equal(EstadoOrdenProduccion.Planificada, (await verificacion.OrdenesProduccion.AsNoTracking().SingleAsync()).Estado);
    }
}
