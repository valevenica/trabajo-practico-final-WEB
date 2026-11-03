using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Movimiento de stock de producto terminado que acompaña a un cambio de estado de una orden. Lo usan la pantalla de
    /// órdenes y el panel principal, para que avanzar un pedido desde cualquiera de los dos tenga el mismo efecto.
    /// </summary>
    public static class OrdenStock
    {
        public static async Task AplicarMovimientoAsync(IUnitOfWork unitOfWork, EstadoOrdenProduccion anterior, OrdenProduccion orden)
        {
            var movimiento = OrdenProduccion.MovimientoDeStock(anterior, orden.Estado);
            if (movimiento == 0) { return; }

            // Primero se buscan y validan todos los productos: si a uno le falta stock, no se mueve ninguno.
            var lineas = new List<(Producto Producto, decimal Cantidad)>();
            foreach (var item in orden.Items)
            {
                var producto = await unitOfWork.Productos.GetByIdAsync(item.ProductoId)
                    ?? throw new InvalidOperationException("No se encontró uno de los productos de la orden.");
                lineas.Add((producto, item.Cantidad));
            }

            if (movimiento < 0)
            {
                var faltantes = lineas
                    .Where(x => x.Producto.StockActual < x.Cantidad)
                    .Select(x => $"{x.Producto.Nombre} (faltan {x.Cantidad - x.Producto.StockActual:0.##} unidades)")
                    .ToList();
                if (faltantes.Count > 0)
                {
                    throw new InvalidOperationException($"Stock insuficiente para completar el pedido: {string.Join(", ", faltantes)}.");
                }
            }

            foreach (var (producto, cantidad) in lineas)
            {
                producto.StockActual += movimiento * cantidad;
                unitOfWork.Productos.Update(producto);
            }
        }
    }
}
