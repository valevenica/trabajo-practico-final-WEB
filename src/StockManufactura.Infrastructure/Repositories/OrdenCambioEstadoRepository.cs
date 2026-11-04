using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;
using StockManufactura.Infrastructure.Db;

namespace StockManufactura.Infrastructure.Repositories
{
    public sealed class OrdenCambioEstadoRepository : Repository<OrdenProduccionCambioEstado>, IOrdenCambioEstadoRepository
    {
        private readonly StockManufacturaDbContext _context;

        public OrdenCambioEstadoRepository(StockManufacturaDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<OrdenProduccionCambioEstado>> ListByOrdenAsync(Guid ordenProduccionId)
        {
            return await _context.OrdenesProduccionCambiosEstado
                .AsNoTracking()
                .Where(x => x.OrdenProduccionId == ordenProduccionId)
                .OrderBy(x => x.Fecha)
                .ToListAsync();
        }
    }
}
