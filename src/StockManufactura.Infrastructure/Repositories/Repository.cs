using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockManufactura.Application.Interfaces;
using StockManufactura.Infrastructure.Db;

namespace StockManufactura.Infrastructure.Repositories
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        private readonly StockManufacturaDbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public Repository(StockManufacturaDbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        public async Task<TEntity?> GetByIdAsync(Guid id)
        {
            return await _dbSet.FindAsync(id);
        }

        public async Task<IEnumerable<TEntity>> ListAsync()
        {
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        public async Task AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual void Update(TEntity entity)
        {
            DetachOtherInstanceWithSameKey(entity);
            _dbSet.Update(entity);
        }

        public virtual void Delete(TEntity entity)
        {
            DetachOtherInstanceWithSameKey(entity);
            _dbSet.Remove(entity);
        }

        /// <summary>
        /// Las pantallas conservan su contexto mientras están abiertas y listan sin seguimiento (<see cref="ListAsync"/>).
        /// Si el contexto ya sigue otra instancia de la misma entidad (por ejemplo, la que se agregó o editó antes en esa
        /// pantalla), EF rechaza registrar la nueva copia. Se suelta la anterior: la instancia recibida trae los datos vigentes.
        /// </summary>
        private void DetachOtherInstanceWithSameKey(TEntity entity)
        {
            var key = _context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey();
            if (key is null)
            {
                return;
            }

            var keyValues = key.Properties.Select(p => p.PropertyInfo?.GetValue(entity)).ToArray();
            var other = _context.ChangeTracker.Entries<TEntity>().FirstOrDefault(entry =>
                !ReferenceEquals(entry.Entity, entity)
                && key.Properties.Select(p => entry.Property(p.Name).CurrentValue).SequenceEqual(keyValues));

            if (other is not null)
            {
                other.State = EntityState.Detached;
            }
        }
    }
}
