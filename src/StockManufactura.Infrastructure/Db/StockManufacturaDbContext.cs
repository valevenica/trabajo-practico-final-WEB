using Microsoft.EntityFrameworkCore;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Infrastructure.Db
{
    public class StockManufacturaDbContext : DbContext
    {
        public StockManufacturaDbContext(DbContextOptions<StockManufacturaDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; } = null!;
        public DbSet<Rol> Roles { get; set; } = null!;
        public DbSet<Proveedor> Proveedores { get; set; } = null!;
        public DbSet<Cliente> Clientes { get; set; } = null!;
        public DbSet<Recurso> Recursos { get; set; } = null!;
        public DbSet<ExchangeRate> ExchangeRates { get; set; } = null!;
        public DbSet<ResourcePriceHistory> ResourcePriceHistory { get; set; } = null!;
        public DbSet<Producto> Productos { get; set; } = null!;
        public DbSet<OrdenProduccion> OrdenesProduccion { get; set; } = null!;
        public DbSet<OrdenProduccionItem> OrdenesProduccionItems { get; set; } = null!;
        public DbSet<RecetaProductoItem> RecetaProductoItems { get; set; } = null!;
        public DbSet<ProductCostHistory> ProductCostHistory { get; set; } = null!;
        public DbSet<ProductCostSnapshot> ProductCostSnapshots { get; set; } = null!;
        public DbSet<ProductCostSnapshotItem> ProductCostSnapshotItems { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<BackupRecord> BackupRecords { get; set; } = null!;
        public DbSet<BackupSettings> BackupSettings { get; set; } = null!;
        public DbSet<RecursoProveedor> RecursoProveedores { get; set; } = null!;
        public DbSet<RecursoTramoPrecio> RecursoTramosPrecio { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockManufacturaDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
