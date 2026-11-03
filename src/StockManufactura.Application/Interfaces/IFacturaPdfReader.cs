using System.IO;
using StockManufactura.Application.Resources;

namespace StockManufactura.Application.Interfaces
{
    /// <summary>Lee la factura de un proveedor en PDF (con texto, como las electrónicas de AFIP).</summary>
    public interface IFacturaPdfReader
    {
        FacturaLeida Leer(Stream pdf);
    }
}
