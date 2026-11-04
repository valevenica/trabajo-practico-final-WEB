using System.Threading.Tasks;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;
using StockManufactura.Infrastructure.Db;

namespace StockManufactura.Infrastructure.Repositories
{
    // All repos are created from the same context so SaveChangesAsync persists what they track.
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly StockManufacturaDbContext _context;

        public UnitOfWork(StockManufacturaDbContext context)
        {
            _context = context;
            Roles = new RolRepository(context);
            Usuarios = new UsuarioRepository(context);
            Proveedores = new ProveedorRepository(context);
            Clientes = new ClienteRepository(context);
            Recursos = new RecursoRepository(context);
            ExchangeRates = new ExchangeRateRepository(context);
            ResourcePriceHistory = new ResourcePriceHistoryRepository(context);
            Productos = new ProductoRepository(context);
            OrdenesProduccion = new OrdenProduccionRepository(context);
            CambiosEstadoOrden = new OrdenCambioEstadoRepository(context);
            RecetaProductoItems = new RecetaProductoItemRepository(context);
            ProductCostHistory = new ProductCostHistoryRepository(context);
            ProductCostSnapshots = new ProductCostSnapshotRepository(context);
            ProductCostSnapshotItems = new ProductCostSnapshotItemRepository(context);
            AuditLogs = new AuditLogRepository(context);
            BackupRecords = new BackupRecordRepository(context);
            BackupSettings = new BackupSettingsRepository(context);
            RecursoProveedores = new RecursoProveedorRepository(context);
            RecursoTramosPrecio = new RecursoTramoPrecioRepository(context);
        }

        public IRolRepository Roles { get; }
        public IUsuarioRepository Usuarios { get; }
        public IProveedorRepository Proveedores { get; }
        public IClienteRepository Clientes { get; }
        public IRecursoRepository Recursos { get; }
        public IExchangeRateRepository ExchangeRates { get; }
        public IResourcePriceHistoryRepository ResourcePriceHistory { get; }
        public IProductoRepository Productos { get; }
        public IOrdenProduccionRepository OrdenesProduccion { get; }
        public IOrdenCambioEstadoRepository CambiosEstadoOrden { get; }
        public IRecetaProductoItemRepository RecetaProductoItems { get; }
        public IProductCostHistoryRepository ProductCostHistory { get; }
        public IProductCostSnapshotRepository ProductCostSnapshots { get; }
        public IProductCostSnapshotItemRepository ProductCostSnapshotItems { get; }
        public IAuditLogRepository AuditLogs { get; }
        public IBackupRecordRepository BackupRecords { get; }
        public IBackupSettingsRepository BackupSettings { get; }
        public IRecursoProveedorRepository RecursoProveedores { get; }
        public IRecursoTramoPrecioRepository RecursoTramosPrecio { get; }

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
