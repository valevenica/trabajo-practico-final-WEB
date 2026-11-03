using System.Collections.Generic;
using System.Threading.Tasks;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Application.Interfaces
{
    public interface IOrdenProduccionRepository : IRepository<OrdenProduccion>
    {
        Task<OrdenProduccion?> GetByCodigoAsync(string codigo);
        Task<IReadOnlyList<OrdenProduccion>> ListByCreatedDescAsync();

        /// <summary>Registra la orden y reemplaza sus productos por los que tiene ahora en <see cref="OrdenProduccion.Items"/>.</summary>
        Task ActualizarConItemsAsync(OrdenProduccion orden);
    }
}
