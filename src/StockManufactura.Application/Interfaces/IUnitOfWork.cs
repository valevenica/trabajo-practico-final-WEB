using System.Threading.Tasks;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Application.Interfaces
{
    public interface IUnitOfWork
    {
        IRolRepository Roles { get; }
        IUsuarioRepository Usuarios { get; }
        IProveedorRepository Proveedores { get; }
        IClienteRepository Clientes { get; }
        IRecursoRepository Recursos { get; }
        IExchangeRateRepository ExchangeRates { get; }
        IResourcePriceHistoryRepository ResourcePriceHistory { get; }
        IProductoRepository Productos { get; }
        IOrdenProduccionRepository OrdenesProduccion { get; }
        IOrdenCambioEstadoRepository CambiosEstadoOrden { get; }
        IRecetaProductoItemRepository RecetaProductoItems { get; }
        IRecursoProveedorRepository RecursoProveedores { get; }
        IRecursoTramoPrecioRepository RecursoTramosPrecio { get; }
        IProductCostHistoryRepository ProductCostHistory { get; }
        IProductCostSnapshotRepository ProductCostSnapshots { get; }
        IProductCostSnapshotItemRepository ProductCostSnapshotItems { get; }
        IAuditLogRepository AuditLogs { get; }
        IBackupRecordRepository BackupRecords { get; }
        IBackupSettingsRepository BackupSettings { get; }
        Task<int> SaveChangesAsync();
    }
}
