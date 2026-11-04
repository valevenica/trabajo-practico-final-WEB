using System;
using StockManufactura.Domain.Entities;
using Xunit;

namespace StockManufactura.UnitTests;

public class ProductionOrderTests
{
    [Fact]
    public void OrdenProduccion_CambiaEstadoYActualizaCantidadProducida()
    {
        var orden = new OrdenProduccion("OP-001", Guid.NewGuid(), 10m, "Orden de prueba");

        orden.MarcarEnProgreso();
        Assert.Equal(EstadoOrdenProduccion.EnProceso, orden.Estado);

        orden.RegistrarProduccion(8m);
        Assert.Equal(8m, orden.CantidadProducida);
        Assert.Equal(EstadoOrdenProduccion.EnProceso, orden.Estado);

        orden.Completar();
        Assert.Equal(EstadoOrdenProduccion.Completada, orden.Estado);
    }

    [Theory]
    [InlineData(EstadoOrdenProduccion.Borrador, EstadoOrdenProduccion.EnProceso, -1)]
    [InlineData(EstadoOrdenProduccion.Planificada, EstadoOrdenProduccion.EnProceso, -1)]
    [InlineData(EstadoOrdenProduccion.Planificada, EstadoOrdenProduccion.Finalizada, -1)]
    [InlineData(EstadoOrdenProduccion.EnProceso, EstadoOrdenProduccion.Planificada, 1)]
    [InlineData(EstadoOrdenProduccion.EnProceso, EstadoOrdenProduccion.Borrador, 1)]
    [InlineData(EstadoOrdenProduccion.EnProceso, EstadoOrdenProduccion.Cancelada, 1)]
    [InlineData(EstadoOrdenProduccion.EnProceso, EstadoOrdenProduccion.Finalizada, 0)]
    [InlineData(EstadoOrdenProduccion.Borrador, EstadoOrdenProduccion.Planificada, 0)]
    [InlineData(EstadoOrdenProduccion.Planificada, EstadoOrdenProduccion.Cancelada, 0)]
    [InlineData(EstadoOrdenProduccion.Cancelada, EstadoOrdenProduccion.Borrador, 0)]
    public void MovimientoDeStock_SegunElCambioDeEstado(EstadoOrdenProduccion anterior, EstadoOrdenProduccion nuevo, int esperado)
    {
        Assert.Equal(esperado, OrdenProduccion.MovimientoDeStock(anterior, nuevo));
    }

    [Fact]
    public void PlanificarUnaOrdenEnProceso_YVolverAIniciarla_DescuentaElStockUnaSolaVez()
    {
        var orden = new OrdenProduccion("OP-002", Guid.NewGuid(), 5m, string.Empty);
        var stock = 20m;
        void Aplicar(Action transicion)
        {
            var anterior = orden.Estado;
            transicion();
            stock += OrdenProduccion.MovimientoDeStock(anterior, orden.Estado) * orden.CantidadPlaneada;
        }

        Aplicar(orden.MarcarEnProgreso);
        Aplicar(orden.Planificar);
        Aplicar(orden.MarcarEnProgreso);

        Assert.Equal(15m, stock);
    }

    [Fact]
    public void OrdenConVariosProductos_AgrupaRepetidosYSumaElTotal()
    {
        var vela = Guid.NewGuid();
        var difusor = Guid.NewGuid();

        var orden = new OrdenProduccion("OP-003", new[] { (vela, 2m), (difusor, 3m), (vela, 1m) }, string.Empty);

        Assert.Equal(2, orden.Items.Count);
        Assert.Equal(3m, Assert.Single(orden.Items, x => x.ProductoId == vela).Cantidad);
        Assert.Equal(6m, orden.CantidadPlaneada);
        Assert.All(orden.Items, x => Assert.Equal(orden.Id, x.OrdenProduccionId));
    }

    [Fact]
    public void OrdenSinProductosOConCantidadCero_SeRechaza()
    {
        Assert.Throws<InvalidOperationException>(() => new OrdenProduccion("OP-004", Array.Empty<(Guid, decimal)>(), string.Empty));
        Assert.Throws<InvalidOperationException>(() => new OrdenProduccion("OP-005", Guid.NewGuid(), 0m, string.Empty));
    }

    [Fact]
    public void ReemplazarProductos_SoloMientrasEstaEnEspera()
    {
        var orden = new OrdenProduccion("OP-006", Guid.NewGuid(), 1m, string.Empty);
        var nuevo = Guid.NewGuid();

        orden.Planificar();
        orden.ReemplazarItems(new[] { (nuevo, 4m) });
        Assert.Equal(nuevo, Assert.Single(orden.Items).ProductoId);

        orden.MarcarEnProgreso();
        Assert.Throws<InvalidOperationException>(() => orden.ReemplazarItems(new[] { (nuevo, 1m) }));
    }

    [Fact]
    public void OrdenFinalizada_SoloPuedeReabrirseAEnProceso()
    {
        var orden = new OrdenProduccion("OP-007", Guid.NewGuid(), 1m, string.Empty);
        orden.MarcarEnProgreso();
        orden.Completar();

        Assert.Throws<InvalidOperationException>(orden.VolverABorrador);
        Assert.Throws<InvalidOperationException>(orden.Planificar);
        Assert.Throws<InvalidOperationException>(orden.MarcarEnProgreso);
        Assert.Throws<InvalidOperationException>(() => orden.Cancelar("x"));

        orden.Reabrir();
        Assert.Equal(EstadoOrdenProduccion.EnProceso, orden.Estado);
        Assert.Null(orden.FechaFin);
        Assert.Equal(0, OrdenProduccion.MovimientoDeStock(EstadoOrdenProduccion.Finalizada, EstadoOrdenProduccion.EnProceso));
    }

    [Theory]
    [InlineData(EstadoOrdenProduccion.Borrador)]
    [InlineData(EstadoOrdenProduccion.Planificada)]
    [InlineData(EstadoOrdenProduccion.EnProceso)]
    public void OrdenCancelada_VuelveAlEstadoQueTeniaAntes(EstadoOrdenProduccion previo)
    {
        var orden = new OrdenProduccion("OP-008", Guid.NewGuid(), 1m, string.Empty);
        if (previo == EstadoOrdenProduccion.Planificada) { orden.Planificar(); }
        if (previo == EstadoOrdenProduccion.EnProceso) { orden.MarcarEnProgreso(); }

        orden.Cancelar("Cliente desistió");
        Assert.Equal(previo, orden.EstadoParaRestaurar);
        Assert.Throws<InvalidOperationException>(orden.VolverABorrador);
        Assert.Throws<InvalidOperationException>(orden.MarcarEnProgreso);

        orden.RestaurarCancelada();
        Assert.Equal(previo, orden.Estado);
        Assert.Null(orden.EstadoAntesDeCancelar);
        Assert.Null(orden.FechaFin);
    }

    [Fact]
    public void CanceladaDesdeEnProceso_YRestaurada_DescuentaElStockDeNuevo()
    {
        var orden = new OrdenProduccion("OP-009", Guid.NewGuid(), 5m, string.Empty);
        var stock = 20m;
        void Aplicar(Action transicion)
        {
            var anterior = orden.Estado;
            transicion();
            stock += OrdenProduccion.MovimientoDeStock(anterior, orden.Estado) * orden.CantidadPlaneada;
        }

        Aplicar(orden.MarcarEnProgreso);
        Aplicar(() => orden.Cancelar("x"));
        Assert.Equal(20m, stock);

        Aplicar(orden.RestaurarCancelada);
        Assert.Equal(EstadoOrdenProduccion.EnProceso, orden.Estado);
        Assert.Equal(15m, stock);
    }

    [Fact]
    public void TiempoDeArmado_SumaLosTramosEnProceso()
    {
        var id = Guid.NewGuid();
        var t0 = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var historial = new[]
        {
            new OrdenProduccionCambioEstado(id, null, EstadoOrdenProduccion.Borrador, "u", t0),
            new OrdenProduccionCambioEstado(id, EstadoOrdenProduccion.Borrador, EstadoOrdenProduccion.EnProceso, "u", t0.AddHours(1)),
            new OrdenProduccionCambioEstado(id, EstadoOrdenProduccion.EnProceso, EstadoOrdenProduccion.Finalizada, "u", t0.AddHours(3)),
            new OrdenProduccionCambioEstado(id, EstadoOrdenProduccion.Finalizada, EstadoOrdenProduccion.EnProceso, "u", t0.AddHours(10)),
            new OrdenProduccionCambioEstado(id, EstadoOrdenProduccion.EnProceso, EstadoOrdenProduccion.Finalizada, "u", t0.AddHours(10.5)),
        };

        Assert.Equal(TimeSpan.FromHours(2.5), OrdenProduccionCambioEstado.TiempoDeArmado(historial));
        Assert.Null(OrdenProduccionCambioEstado.TiempoDeArmado(historial[..2]));
    }
}
