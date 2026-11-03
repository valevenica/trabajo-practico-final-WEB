using System;

namespace StockManufactura.Domain.Entities
{
    /// <summary>Línea de una orden: un producto y la cantidad pedida de ese producto.</summary>
    public sealed class OrdenProduccionItem : BaseEntity
    {
        public OrdenProduccionItem(Guid ordenProduccionId, Guid productoId, decimal cantidad)
        {
            if (cantidad <= 0)
            {
                throw new InvalidOperationException("La cantidad de cada producto de la orden debe ser mayor a cero.");
            }

            OrdenProduccionId = ordenProduccionId;
            ProductoId = productoId;
            Cantidad = cantidad;
        }

        private OrdenProduccionItem() { }

        public Guid OrdenProduccionId { get; private set; }
        public Guid ProductoId { get; private set; }
        public decimal Cantidad { get; private set; }
    }
}
