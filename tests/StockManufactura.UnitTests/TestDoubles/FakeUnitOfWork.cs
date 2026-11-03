#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;

namespace StockManufactura.UnitTests.TestDoubles;

/// <summary>
/// Unit of Work en memoria compartido por todas las pruebas. Cada repositorio guarda las
/// entidades en una lista, de modo que una prueba puede sembrar datos y verificar lo que
/// el servicio bajo prueba escribio.
///
/// Vive en un unico archivo a proposito: cuando cada clase de prueba mantenia su propia copia
/// de los stubs, agregar un repositorio a IUnitOfWork rompia la compilacion en varios lugares.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool SaveCalled { get; private set; }
    public int SaveCount { get; private set; }

    public FakeRolRepository RolesFake { get; } = new();
    public FakeUsuarioRepository UsuariosFake { get; } = new();
    public FakeProveedorRepository ProveedoresFake { get; } = new();
    public FakeClienteRepository ClientesFake { get; } = new();
    public FakeRecursoRepository RecursosFake { get; } = new();
    public FakeExchangeRateRepository ExchangeRatesFake { get; } = new();
    public FakeResourcePriceHistoryRepository ResourcePriceHistoryFake { get; } = new();
    public FakeProductoRepository ProductosFake { get; } = new();
    public FakeOrdenProduccionRepository OrdenesProduccionFake { get; } = new();
    public FakeRecetaProductoItemRepository RecetaProductoItemsFake { get; } = new();
    public FakeRecursoProveedorRepository RecursoProveedoresFake { get; } = new();
    public FakeRecursoTramoPrecioRepository RecursoTramosPrecioFake { get; } = new();
    public FakeProductCostHistoryRepository ProductCostHistoryFake { get; } = new();
    public FakeProductCostSnapshotRepository ProductCostSnapshotsFake { get; } = new();
    public FakeProductCostSnapshotItemRepository ProductCostSnapshotItemsFake { get; } = new();
    public FakeAuditLogRepository AuditLogsFake { get; } = new();
    public FakeBackupRecordRepository BackupRecordsFake { get; } = new();
    public FakeBackupSettingsRepository BackupSettingsFake { get; } = new();

    public IRolRepository Roles => RolesFake;
    public IUsuarioRepository Usuarios => UsuariosFake;
    public IProveedorRepository Proveedores => ProveedoresFake;
    public IClienteRepository Clientes => ClientesFake;
    public IRecursoRepository Recursos => RecursosFake;
    public IExchangeRateRepository ExchangeRates => ExchangeRatesFake;
    public IResourcePriceHistoryRepository ResourcePriceHistory => ResourcePriceHistoryFake;
    public IProductoRepository Productos => ProductosFake;
    public IOrdenProduccionRepository OrdenesProduccion => OrdenesProduccionFake;
    public IRecetaProductoItemRepository RecetaProductoItems => RecetaProductoItemsFake;
    public IRecursoProveedorRepository RecursoProveedores => RecursoProveedoresFake;
    public IRecursoTramoPrecioRepository RecursoTramosPrecio => RecursoTramosPrecioFake;
    public IProductCostHistoryRepository ProductCostHistory => ProductCostHistoryFake;
    public IProductCostSnapshotRepository ProductCostSnapshots => ProductCostSnapshotsFake;
    public IProductCostSnapshotItemRepository ProductCostSnapshotItems => ProductCostSnapshotItemsFake;
    public IAuditLogRepository AuditLogs => AuditLogsFake;
    public IBackupRecordRepository BackupRecords => BackupRecordsFake;
    public IBackupSettingsRepository BackupSettings => BackupSettingsFake;

    public Task<int> SaveChangesAsync()
    {
        SaveCalled = true;
        SaveCount++;
        return Task.FromResult(0);
    }
}

public class FakeRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    public List<TEntity> Items { get; } = new();

    public FakeRepository<TEntity> Seed(params TEntity[] entities)
    {
        Items.AddRange(entities);
        return this;
    }

    public Task<TEntity?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<IEnumerable<TEntity>> ListAsync() => Task.FromResult<IEnumerable<TEntity>>(Items.ToArray());

    public Task AddAsync(TEntity entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity)
    {
        if (!Items.Contains(entity))
        {
            Items.Add(entity);
        }
    }

    public void Delete(TEntity entity) => Items.Remove(entity);
}

public sealed class FakeRolRepository : FakeRepository<Rol>, IRolRepository
{
}

public sealed class FakeUsuarioRepository : FakeRepository<Usuario>, IUsuarioRepository
{
    public Task<Usuario?> GetByEmailAsync(string email, bool includeRole = false)
        => Task.FromResult(Items.FirstOrDefault(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario?> GetByNombreAsync(string nombre, bool includeRole = false)
        => Task.FromResult(Items.FirstOrDefault(x => x.Nombre.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<Usuario?> GetByIdWithRoleAsync(Guid id) => GetByIdAsync(id);

    public Task<IReadOnlyList<Usuario>> ListWithRoleAsync() => Task.FromResult<IReadOnlyList<Usuario>>(Items.ToArray());
}

public sealed class FakeClienteRepository : FakeRepository<Cliente>, IClienteRepository
{
    public Task<Cliente?> GetByDocumentoAsync(string documento)
        => Task.FromResult(Items.FirstOrDefault(x => x.Documento == documento));
}

public sealed class FakeProveedorRepository : FakeRepository<Proveedor>, IProveedorRepository
{
    public Task<Proveedor?> GetByCuitAsync(string cuit)
        => Task.FromResult(Items.FirstOrDefault(x => x.Cuit == cuit));
}

public sealed class FakeRecursoRepository : FakeRepository<Recurso>, IRecursoRepository
{
    public Task<Recurso?> GetByCodigoAsync(string codigo)
        => Task.FromResult(Items.FirstOrDefault(x => x.Codigo == codigo));

    public Task<IReadOnlyList<Recurso>> ListActivosAsync()
        => Task.FromResult<IReadOnlyList<Recurso>>(Items.Where(x => x.Activo).ToArray());
}

public sealed class FakeExchangeRateRepository : FakeRepository<ExchangeRate>, IExchangeRateRepository
{
    public Task<ExchangeRate?> GetLatestAsync()
        => Task.FromResult(Items.OrderByDescending(x => x.Fecha).FirstOrDefault());

    public Task SetPrioritariaAsync(Guid id)
    {
        foreach (var rate in Items)
        {
            rate.EsPrioritaria = rate.Id == id;
        }

        return Task.CompletedTask;
    }
}

public sealed class FakeResourcePriceHistoryRepository : FakeRepository<ResourcePriceHistory>, IResourcePriceHistoryRepository
{
    public Task<IReadOnlyList<ResourcePriceHistory>> ListByResourceAsync(Guid recursoId)
        => Task.FromResult<IReadOnlyList<ResourcePriceHistory>>(Items.Where(x => x.RecursoId == recursoId).ToArray());
}

public sealed class FakeProductoRepository : FakeRepository<Producto>, IProductoRepository
{
    public Task<Producto?> GetByCodigoAsync(string codigo)
        => Task.FromResult(Items.FirstOrDefault(x => x.Codigo == codigo));

    public Task<IReadOnlyList<Producto>> ListActivosAsync()
        => Task.FromResult<IReadOnlyList<Producto>>(Items.Where(x => x.Activo).ToArray());

    public Task<IReadOnlyList<Producto>> ListByIdsAsync(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        return Task.FromResult<IReadOnlyList<Producto>>(Items.Where(x => set.Contains(x.Id)).ToArray());
    }
}

public sealed class FakeOrdenProduccionRepository : FakeRepository<OrdenProduccion>, IOrdenProduccionRepository
{
    public Task<OrdenProduccion?> GetByCodigoAsync(string codigo)
        => Task.FromResult(Items.FirstOrDefault(x => x.Codigo == codigo));

    public Task<IReadOnlyList<OrdenProduccion>> ListByCreatedDescAsync()
        => Task.FromResult<IReadOnlyList<OrdenProduccion>>(Items.OrderByDescending(x => x.CreatedAt).ToArray());

    public Task ActualizarConItemsAsync(OrdenProduccion orden) => Task.CompletedTask;
}

public sealed class FakeRecetaProductoItemRepository : FakeRepository<RecetaProductoItem>, IRecetaProductoItemRepository
{
    public Task<IReadOnlyList<RecetaProductoItem>> ListByProductIdAsync(Guid productId)
        => Task.FromResult<IReadOnlyList<RecetaProductoItem>>(Items.Where(x => x.ProductoId == productId).ToArray());

    public Task<IReadOnlyList<RecetaProductoItem>> ListByProductIdsAsync(IEnumerable<Guid> productIds)
    {
        var set = productIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<RecetaProductoItem>>(Items.Where(x => set.Contains(x.ProductoId)).ToArray());
    }

    public Task<IReadOnlyList<RecetaProductoItem>> ListByResourceIdAsync(Guid resourceId)
        => Task.FromResult<IReadOnlyList<RecetaProductoItem>>(Items.Where(x => x.RecursoId == resourceId).ToArray());
}

public sealed class FakeRecursoProveedorRepository : FakeRepository<RecursoProveedor>, IRecursoProveedorRepository
{
    public Task<IReadOnlyList<RecursoProveedor>> ListByRecursoIdAsync(Guid recursoId)
        => Task.FromResult<IReadOnlyList<RecursoProveedor>>(Items.Where(x => x.RecursoId == recursoId).ToArray());

    public Task<RecursoProveedor?> GetPrioritarioAsync(Guid recursoId)
        => Task.FromResult(Items.FirstOrDefault(x => x.RecursoId == recursoId && x.EsPrioritario));

    public Task<RecursoProveedor?> GetByProveedorAndCodigoAsync(Guid proveedorId, string codigo)
        => Task.FromResult(Items.FirstOrDefault(x => x.ProveedorId == proveedorId && x.CodigoProveedor == codigo));
}

public sealed class FakeRecursoTramoPrecioRepository : FakeRepository<RecursoTramoPrecio>, IRecursoTramoPrecioRepository
{
    public Task<IReadOnlyList<RecursoTramoPrecio>> ListByRecursoIdAsync(Guid recursoId)
        => Task.FromResult<IReadOnlyList<RecursoTramoPrecio>>(Items.Where(x => x.RecursoId == recursoId).OrderBy(x => x.Orden).ToArray());

    public Task DeleteByRecursoIdAsync(Guid recursoId)
    {
        Items.RemoveAll(x => x.RecursoId == recursoId);
        return Task.CompletedTask;
    }
}

public sealed class FakeProductCostHistoryRepository : FakeRepository<ProductCostHistory>, IProductCostHistoryRepository
{
    public Task<IReadOnlyList<ProductCostHistory>> ListByProductAsync(Guid productId)
        => Task.FromResult<IReadOnlyList<ProductCostHistory>>(Items.Where(x => x.ProductId == productId).ToArray());
}

public sealed class FakeProductCostSnapshotRepository : FakeRepository<ProductCostSnapshot>, IProductCostSnapshotRepository
{
    public Task<IReadOnlyList<ProductCostSnapshot>> ListByProductAsync(Guid productId)
        => Task.FromResult<IReadOnlyList<ProductCostSnapshot>>(Items.Where(x => x.ProductId == productId).ToArray());
}

public sealed class FakeProductCostSnapshotItemRepository : FakeRepository<ProductCostSnapshotItem>, IProductCostSnapshotItemRepository
{
    public Task<IReadOnlyList<ProductCostSnapshotItem>> ListBySnapshotAsync(Guid snapshotId)
        => Task.FromResult<IReadOnlyList<ProductCostSnapshotItem>>(Items.Where(x => x.SnapshotId == snapshotId).ToArray());
}

public sealed class FakeAuditLogRepository : FakeRepository<AuditLog>, IAuditLogRepository
{
    public Task<IReadOnlyList<AuditLog>> QueryAsync(DateTime? from, DateTime? to, string? usuario, string? modulo, string? accion)
    {
        var query = Items.AsEnumerable();
        if (from.HasValue) query = query.Where(x => x.FechaHora >= from.Value);
        if (to.HasValue) query = query.Where(x => x.FechaHora <= to.Value);
        if (!string.IsNullOrWhiteSpace(usuario)) query = query.Where(x => x.Usuario == usuario);
        if (!string.IsNullOrWhiteSpace(modulo)) query = query.Where(x => x.Modulo == modulo);
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(x => x.Accion == accion);
        return Task.FromResult<IReadOnlyList<AuditLog>>(query.OrderByDescending(x => x.FechaHora).ToArray());
    }

    public Task<IReadOnlyList<AuditLog>> ListByProductIdAsync(Guid productId)
        => Task.FromResult<IReadOnlyList<AuditLog>>(Items.Where(x => x.IdEntidad == productId.ToString()).ToArray());
}

public sealed class FakeBackupRecordRepository : FakeRepository<BackupRecord>, IBackupRecordRepository
{
    public Task<IReadOnlyList<BackupRecord>> ListRecentAsync(int top)
        => Task.FromResult<IReadOnlyList<BackupRecord>>(Items.OrderByDescending(x => x.FechaHora).Take(top).ToArray());
}

public sealed class FakeBackupSettingsRepository : FakeRepository<BackupSettings>, IBackupSettingsRepository
{
    public Task<BackupSettings?> GetCurrentAsync() => Task.FromResult(Items.FirstOrDefault());
}
