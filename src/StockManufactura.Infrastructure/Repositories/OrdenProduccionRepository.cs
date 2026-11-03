using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;
using StockManufactura.Infrastructure.Db;

namespace StockManufactura.Infrastructure.Repositories
{
    public sealed class OrdenProduccionRepository : Repository<OrdenProduccion>, IOrdenProduccionRepository
    {
        private readonly StockManufacturaDbContext _context;

        public OrdenProduccionRepository(StockManufacturaDbContext context) : base(context)
        {
            _context = context;
        }

        public Task<OrdenProduccion?> GetByCodigoAsync(string codigo)
        {
            return _context.OrdenesProduccion.FirstOrDefaultAsync(x => x.Codigo == codigo);
        }

        public async Task<IReadOnlyList<OrdenProduccion>> ListByCreatedDescAsync()
        {
            return await _context.OrdenesProduccion
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public override void Update(OrdenProduccion entity)
        {
            SoltarItemsSeguidos(entity.Id);
            base.Update(entity);
        }

        public override void Delete(OrdenProduccion entity)
        {
            SoltarItemsSeguidos(entity.Id);
            base.Delete(entity);
        }

        public async Task ActualizarConItemsAsync(OrdenProduccion orden)
        {
            SoltarItemsSeguidos(orden.Id);
            var entrada = _context.ChangeTracker.Entries<OrdenProduccion>().FirstOrDefault(x => x.Entity.Id == orden.Id);
            if (entrada is not null && !ReferenceEquals(entrada.Entity, orden))
            {
                entrada.State = EntityState.Detached;
            }

            var anteriores = await _context.OrdenesProduccionItems.AsNoTracking().Where(x => x.OrdenProduccionId == orden.Id).ToListAsync();
            var idsAnteriores = anteriores.Select(x => x.Id).ToHashSet();

            _context.Entry(orden).State = EntityState.Modified;
            foreach (var item in orden.Items)
            {
                _context.Entry(item).State = idsAnteriores.Contains(item.Id) ? EntityState.Unchanged : EntityState.Added;
            }

            foreach (var quitado in anteriores.Where(x => orden.Items.All(i => i.Id != x.Id)))
            {
                _context.OrdenesProduccionItems.Remove(quitado);
            }
        }

        /// <summary>
        /// Las pantallas leen las órdenes sin seguimiento; si el contexto ya sigue líneas de esta orden (de una edición
        /// anterior), se sueltan para que no choquen con las de la instancia que se va a registrar.
        /// </summary>
        private void SoltarItemsSeguidos(System.Guid ordenId)
        {
            foreach (var entrada in _context.ChangeTracker.Entries<OrdenProduccionItem>()
                         .Where(x => x.Entity.OrdenProduccionId == ordenId && x.State != EntityState.Added)
                         .ToList())
            {
                entrada.State = EntityState.Detached;
            }
        }
    }
}
