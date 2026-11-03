using System;
using StockManufactura.Application.Resources;
using Xunit;

namespace StockManufactura.UnitTests;

public class FacturaTextoParserTests
{
    private static readonly string[] FacturaB =
    {
        "ORIGINAL",
        "Ceras del Litoral S.A.  FACTURA",
        "Razón Social: Ceras del Litoral S.A.  Punto de Venta: 00003 Comp. Nro: 00001542",
        "Fecha de Emisión: 03/11/2026",
        "CUIT: 30-70555444-1",
        "CUIT: 20-12345678-9  Apellido y Nombre / Razón Social: StockManufactura",
        "Código  Producto / Servicio  Cantidad  U. Medida  Precio Unit.  % Bonif  Imp. Bonif.  Subtotal",
        "CL-CERA-01  Cera de soja natural  25,00  kilogramos  11.800,00  0,00  0,00  295.000,00",
        "CL-MECHA-15  Mecha de algodón encerada 15 cm  200,00  unidades  32,50  5,00  325,00  6.175,00",
        "Frasco ámbar 220ml con tapa  48,00  unidades  205,00  0,00  0,00  9.840,00",
        "Subtotal: $ 311.015,00",
        "Importe Total: $ 311.015,00"
    };

    [Fact]
    public void Encabezado_TomaElCuitDelEmisorComprobanteYFecha()
    {
        var factura = FacturaTextoParser.Parsear(FacturaB);

        Assert.Equal("30705554441", factura.CuitEmisor);
        Assert.Equal("Ceras del Litoral S.A.", factura.RazonSocialEmisor);
        Assert.Equal("00003-00001542", factura.NumeroComprobante);
        Assert.Equal(new DateTime(2026, 11, 3), factura.Fecha);
    }

    [Fact]
    public void Renglones_LeeCodigoCantidadYPrecioAunqueLaDescripcionTengaNumeros()
    {
        var factura = FacturaTextoParser.Parsear(FacturaB);

        Assert.Equal(3, factura.Renglones.Count);

        var cera = factura.Renglones[0];
        Assert.Equal("CL-CERA-01", cera.Codigo);
        Assert.Equal("Cera de soja natural", cera.Descripcion);
        Assert.Equal(25m, cera.Cantidad);
        Assert.Equal("kilogramos", cera.Unidad);
        Assert.Equal(11800m, cera.PrecioUnitario);
        Assert.Equal(295000m, cera.Subtotal);

        var mecha = factura.Renglones[1];
        Assert.Equal("Mecha de algodón encerada 15 cm", mecha.Descripcion);
        Assert.Equal(200m, mecha.Cantidad);
        Assert.Equal(32.5m, mecha.PrecioUnitario);

        // Sin código: la descripción empieza en la primera palabra.
        var frasco = factura.Renglones[2];
        Assert.Equal(string.Empty, frasco.Codigo);
        Assert.Equal("Frasco ámbar 220ml con tapa", frasco.Descripcion);
        Assert.Equal(48m, frasco.Cantidad);
    }

    [Fact]
    public void FacturaA_ConAlicuotaDeIva_LeeElRenglon()
    {
        Assert.True(FacturaTextoParser.TryParsearRenglon("CL-PARAF-05  Parafina en escamas  10,00  kilogramos  4.500,00  0,00  0,00  45.000,00  21%  54.450,00", out var renglon));

        Assert.Equal("CL-PARAF-05", renglon.Codigo);
        Assert.Equal(10m, renglon.Cantidad);
        Assert.Equal(4500m, renglon.PrecioUnitario);
        Assert.Equal(54450m, renglon.Subtotal);
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234,5", 1234.5)]
    [InlineData("1234.56", 1234.56)]
    [InlineData("12.500", 12500)]
    public void Importes_EnFormatoArgentinoOInternacional(string texto, double esperado)
    {
        Assert.Equal((decimal)esperado, FacturaTextoParser.ParsearImporte(texto));
    }

    [Fact]
    public void LineasQueNoSonRenglones_SeIgnoran()
    {
        Assert.False(FacturaTextoParser.TryParsearRenglon("Condición frente al IVA: Responsable Inscripto", out _));
        Assert.False(FacturaTextoParser.TryParsearRenglon("Página 1 de 1", out _));
    }
}
