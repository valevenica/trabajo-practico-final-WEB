using StockManufactura.Domain.Entities;
using StockManufactura.UnitTests.TestDoubles;
using StockManufactura.Web.Infrastructure;
using Xunit;

namespace StockManufactura.Web.Tests;

/// <summary>
/// El panel principal y la pantalla de órdenes comparten este movimiento: un pedido que pasa a "En proceso" descuenta el
/// producto terminado y uno que vuelve a espera o se cancela lo reintegra.
/// </summary>
public class OrdenStockTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();

    private (Producto Producto, OrdenProduccion Orden) Sembrar(decimal stock, decimal cantidad)
    {
        var producto = new Producto { Codigo = "PRD-1", Nombre = "Vela", StockActual = stock };
        _unitOfWork.ProductosFake.Seed(producto);
        var orden = new OrdenProduccion("OP-1", producto.Id, cantidad, "Pedido de prueba");
        return (producto, orden);
    }

    [Fact]
    public async Task IniciarPedido_DescuentaElStockDelProducto()
    {
        var (producto, orden) = Sembrar(stock: 10, cantidad: 4);
        orden.Planificar();

        orden.MarcarEnProgreso();
        await OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.Planificada, orden);

        Assert.Equal(6, producto.StockActual);
    }

    [Fact]
    public async Task CancelarPedidoEnProceso_ReintegraElStock()
    {
        var (producto, orden) = Sembrar(stock: 6, cantidad: 4);
        orden.MarcarEnProgreso();

        orden.Cancelar("Cliente desistió");
        await OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.EnProceso, orden);

        Assert.Equal(10, producto.StockActual);
    }

    [Fact]
    public async Task StockInsuficiente_RechazaElMovimientoSinTocarElProducto()
    {
        var (producto, orden) = Sembrar(stock: 2, cantidad: 5);
        orden.MarcarEnProgreso();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.Planificada, orden));

        Assert.Contains("faltan 3", error.Message);
        Assert.Equal(2, producto.StockActual);
    }

    [Fact]
    public async Task PedidoConVariosProductos_MueveCadaUnoYSiFaltaUnoNoTocaNinguno()
    {
        var vela = new Producto { Codigo = "PRD-1", Nombre = "Vela", StockActual = 10 };
        var difusor = new Producto { Codigo = "PRD-2", Nombre = "Difusor", StockActual = 3 };
        _unitOfWork.ProductosFake.Seed(vela);
        _unitOfWork.ProductosFake.Seed(difusor);

        var orden = new OrdenProduccion("OP-3", new[] { (vela.Id, 4m), (difusor.Id, 2m) }, string.Empty);
        orden.MarcarEnProgreso();
        await OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.Planificada, orden);
        Assert.Equal(6, vela.StockActual);
        Assert.Equal(1, difusor.StockActual);

        var grande = new OrdenProduccion("OP-4", new[] { (vela.Id, 1m), (difusor.Id, 5m) }, string.Empty);
        grande.MarcarEnProgreso();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.Planificada, grande));
        Assert.Contains("Difusor", error.Message);
        Assert.Equal(6, vela.StockActual);
        Assert.Equal(1, difusor.StockActual);
    }

    [Fact]
    public async Task FinalizarPedidoEnProceso_NoVuelveADescontar()
    {
        var (producto, orden) = Sembrar(stock: 6, cantidad: 4);
        orden.MarcarEnProgreso();

        orden.Completar();
        await OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.EnProceso, orden);

        Assert.Equal(6, producto.StockActual);
    }

    [Fact]
    public async Task ProductoInexistente_InformaElError()
    {
        var orden = new OrdenProduccion("OP-2", Guid.NewGuid(), 1, "Sin producto");
        orden.MarcarEnProgreso();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => OrdenStock.AplicarMovimientoAsync(_unitOfWork, EstadoOrdenProduccion.Borrador, orden));
    }
}
