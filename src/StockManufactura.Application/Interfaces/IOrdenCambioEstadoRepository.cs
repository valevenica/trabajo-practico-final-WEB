using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Application.Interfaces
{
    public interface IOrdenCambioEstadoRepository : IRepository<OrdenProduccionCambioEstado>
    {
        /// <summary>Historial de estados de una orden, del más antiguo al más reciente.</summary>
        Task<IReadOnlyList<OrdenProduccionCambioEstado>> ListByOrdenAsync(Guid ordenProduccionId);
    }
}
