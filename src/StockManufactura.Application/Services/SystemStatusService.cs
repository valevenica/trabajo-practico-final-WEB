using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;
using StockManufactura.Shared;

namespace StockManufactura.Application.Services
{
    public sealed class SystemStatusService : ISystemStatusService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMonetaryConfigurationService _monetaryConfigurationService;
        private readonly IBackupService _backupService;
        private readonly IShortageService _shortageService;
        private readonly ICloudBackupSyncService? _cloudSyncService;

        public SystemStatusService(
            IUnitOfWork unitOfWork,
            IMonetaryConfigurationService monetaryConfigurationService,
            IBackupService backupService,
            IShortageService shortageService,
            ICloudBackupSyncService? cloudSyncService = null)
        {
            _unitOfWork = unitOfWork;
            _monetaryConfigurationService = monetaryConfigurationService;
            _backupService = backupService;
            _shortageService = shortageService;
            _cloudSyncService = cloudSyncService;
        }

        public Task<SystemStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return GetSnapshotAsync(now.Year, now.Month, cancellationToken);
        }

        public async Task<SystemStatusSnapshot> GetSnapshotAsync(int? year, int? month, CancellationToken cancellationToken = default)
        {
            var products = await _unitOfWork.Productos.ListAsync();
            var resources = await _unitOfWork.Recursos.ListAsync();
            var orders = await _unitOfWork.OrdenesProduccion.ListAsync();
            var costHistory = await _unitOfWork.ProductCostHistory.ListAsync();
            var priceHistory = await _unitOfWork.ResourcePriceHistory.ListAsync();
            var auditLogs = await _unitOfWork.AuditLogs.ListAsync();
            var customers = await _unitOfWork.Usuarios.ListAsync();
            var latestBackup = (await _backupService.GetRecentBackupsAsync(1, cancellationToken)).FirstOrDefault();
            var monetaryState = await _monetaryConfigurationService.GetCurrentStateAsync(cancellationToken);
            var settings = await _backupService.GetSettingsAsync(cancellationToken);

            var databasePath = AppPaths.DatabaseFilePath;
            var databaseSizeBytes = File.Exists(databasePath) ? new FileInfo(databasePath).Length : 0;
            var backupEnabled = settings.Automatico || settings.GoogleDriveHabilitado || latestBackup is not null;
            var driveSyncEnabled = settings.GoogleDriveHabilitado && _cloudSyncService is not null;
            var cloudAvailable = driveSyncEnabled && await _cloudSyncService!.IsAvailableAsync(settings, cancellationToken);
            var backupStatus = latestBackup is null ? "Sin respaldo reciente" : "Activo";
            var cloudProvider = driveSyncEnabled
                ? (string.IsNullOrWhiteSpace(settings.ProveedorNube) ? _cloudSyncService!.ProviderName : settings.ProveedorNube)
                : "No configurado";
            var cloudSyncStatus = !driveSyncEnabled
                ? "Deshabilitada"
                : cloudAvailable
                    ? (settings.SincronizacionBidireccional ? "Bidireccional activa" : "Solo subida")
                    : "Destino no disponible";
            var statusTone = latestBackup is null ? "Warning" : "Success";
            var shortageReport = await _shortageService.GetShortagesAsync(cancellationToken);
            var criticalStockCount = shortageReport.TotalCount;
            var ordersInProcessCount = orders.Count(order => order.Estado == EstadoOrdenProduccion.EnProceso);

            var monthlyCostTotal = costHistory
                .Where(history => IsInPeriod(history.Fecha, year, month))
                .Sum(history => history.CostoNuevo);

            var productsById = products.ToDictionary(product => product.Id, product => product);
            var finishedInPeriod = FinishedIn(orders, year, month);
            var monthlySalesTotal = SalesOf(finishedInPeriod, productsById);
            var monthlySalesCost = finishedInPeriod
                .SelectMany(order => order.Items)
                .Sum(item => (productsById.TryGetValue(item.ProductoId, out var product) ? product.CostoFabricacionActual : 0m) * item.Cantidad);

            decimal? previousSales = null;
            decimal? salesVariation = null;
            if (year is not null && month is not null)
            {
                var previous = new DateTime(year.Value, month.Value, 1).AddMonths(-1);
                previousSales = SalesOf(FinishedIn(orders, previous.Year, previous.Month), productsById);
                if (previousSales > 0)
                {
                    salesVariation = Math.Round((monthlySalesTotal - previousSales.Value) * 100m / previousSales.Value, 1);
                }
            }

            var ordersInPeriodCount = orders.Count(order =>
                order.Estado != EstadoOrdenProduccion.Cancelada && IsInPeriod(order.CreatedAt, year, month));
            var openOrdersCount = orders.Count(order =>
                order.Estado is EstadoOrdenProduccion.Borrador or EstadoOrdenProduccion.Planificada or EstadoOrdenProduccion.EnProceso);

            var auditsInPeriod = auditLogs.Where(log => IsInPeriod(log.FechaHora, year, month)).ToArray();
            var lastAudit = auditLogs.OrderByDescending(log => log.FechaHora).FirstOrDefault();
            var activeProducts = products.Where(product => product.Activo).ToArray();
            var tracedProductIds = costHistory.Select(history => history.ProductId).ToHashSet();
            var traceabilityCoverage = activeProducts.Length == 0
                ? 0m
                : Math.Round(activeProducts.Count(product => tracedProductIds.Contains(product.Id)) * 100m / activeProducts.Length, 1);

            return new SystemStatusSnapshot
            {
                ProductCount = products.Count(),
                ResourceCount = resources.Count(),
                CustomerCount = customers.Count(),
                CriticalStockCount = criticalStockCount,
                OrdersInProcessCount = ordersInProcessCount,
                MonthlyCostTotal = monthlyCostTotal,
                MonthlySalesTotal = monthlySalesTotal,
                MonthlySalesCost = monthlySalesCost,
                OrdersInPeriodCount = ordersInPeriodCount,
                OpenOrdersCount = openOrdersCount,
                FinishedOrdersInPeriodCount = finishedInPeriod.Length,
                AverageTicket = finishedInPeriod.Length == 0 ? 0m : Math.Round(monthlySalesTotal / finishedInPeriod.Length, 2),
                GrossMarginPercent = monthlySalesTotal > 0 ? Math.Round((monthlySalesTotal - monthlySalesCost) * 100m / monthlySalesTotal, 1) : null,
                PreviousPeriodSalesTotal = previousSales,
                SalesVariationPercent = salesVariation,
                LastBackupAt = latestBackup?.FechaHora,
                LastDollarUpdateAt = monetaryState?.LastUpdate,
                LastDollarSource = monetaryState?.Source ?? string.Empty,
                DatabaseSizeBytes = databaseSizeBytes,
                IsInternetConnected = true,
                ApplicationVersion = "1.0.0",
                LastDriveSyncAt = settings.UltimaSincronizacionNube,
                BackupEnabled = backupEnabled,
                DriveSyncEnabled = driveSyncEnabled,
                BackupStatus = backupStatus,
                CloudProvider = cloudProvider,
                CloudBidirectionalEnabled = driveSyncEnabled && settings.SincronizacionBidireccional,
                CloudSyncStatus = cloudSyncStatus,
                StatusTone = statusTone,
                AuditEventCount = auditsInPeriod.Length,
                AuditUserCount = auditsInPeriod.Select(log => log.Usuario).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                LastAuditAt = lastAudit?.FechaHora,
                LastAuditDescription = lastAudit is null ? string.Empty : $"{lastAudit.Modulo} · {lastAudit.Accion}",
                PriceChangeCount = priceHistory.Count(history => IsInPeriod(history.Fecha, year, month)),
                CostRecalculationCount = costHistory.Count(history => IsInPeriod(history.Fecha, year, month)),
                TraceabilityCoveragePercent = traceabilityCoverage
            };
        }

        private static OrdenProduccion[] FinishedIn(IEnumerable<OrdenProduccion> orders, int? year, int? month)
            => orders
                .Where(order => order.Estado == EstadoOrdenProduccion.Finalizada)
                .Where(order => IsInPeriod(order.FechaFin ?? order.CreatedAt, year, month))
                .ToArray();

        private static decimal SalesOf(IEnumerable<OrdenProduccion> finished, IReadOnlyDictionary<Guid, Producto> productsById)
            => finished.SelectMany(order => order.Items)
                .Sum(item => (productsById.TryGetValue(item.ProductoId, out var product) ? product.PrecioSugeridoActual : 0m) * item.Cantidad);

        private static bool IsInPeriod(DateTime value, int? year, int? month)
        {
            if (year is null || month is null)
            {
                return true;
            }

            return value.Year == year.Value && value.Month == month.Value;
        }
    }
}
