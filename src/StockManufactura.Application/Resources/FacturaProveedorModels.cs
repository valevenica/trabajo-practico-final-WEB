using System;
using System.Collections.Generic;

namespace StockManufactura.Application.Resources
{
    /// <summary>Datos leídos de la factura de un proveedor, antes de cruzarlos con los insumos del sistema.</summary>
    public sealed class FacturaLeida
    {
        public string CuitEmisor { get; set; } = string.Empty;
        public string RazonSocialEmisor { get; set; } = string.Empty;
        public string NumeroComprobante { get; set; } = string.Empty;
        public DateTime? Fecha { get; set; }
        public List<FacturaRenglon> Renglones { get; } = new();
    }

    public sealed class FacturaRenglon
    {
        /// <summary>Código del producto según el proveedor; vacío si la factura no lo trae.</summary>
        public string Codigo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
