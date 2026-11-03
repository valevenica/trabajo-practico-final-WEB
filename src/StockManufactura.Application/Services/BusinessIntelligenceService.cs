using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StockManufactura.Application.Interfaces;
using StockManufactura.Application.Reports;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Application.Services
{
    public sealed class BusinessIntelligenceService : IBusinessIntelligenceService
    {
        private const int TopItems = 8;
        private const int MesesEvolucion = 6;

        private static string EtiquetaEstado(EstadoOrdenProduccion estado) => estado switch
        {
            EstadoOrdenProduccion.Borrador => "Borrador",
            EstadoOrdenProduccion.Planificada => "Planificada",
            EstadoOrdenProduccion.EnProceso => "En proceso",
            EstadoOrdenProduccion.Finalizada => "Finalizada",
            EstadoOrdenProduccion.Cancelada => "Cancelada",
            _ => estado.ToString()
        };

        private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMonetaryConfigurationService _monetaryConfigurationService;
        private readonly IShortageService _shortageService;

        public BusinessIntelligenceService(
            IUnitOfWork unitOfWork,
            IMonetaryConfigurationService monetaryConfigurationService,
            IShortageService shortageService)
        {
            _unitOfWork = unitOfWork;
            _monetaryConfigurationService = monetaryConfigurationService;
            _shortageService = shortageService;
        }

        public async Task<BusinessIntelligenceReport> GetReportAsync(int? year, int? month, CancellationToken cancellationToken = default)
        {
            var productos = (await _unitOfWork.Productos.ListAsync()).ToArray();
            var recursos = (await _unitOfWork.Recursos.ListAsync()).ToArray();
            var ordenes = (await _unitOfWork.OrdenesProduccion.ListAsync()).ToArray();
            var costHistory = (await _unitOfWork.ProductCostHistory.ListAsync()).ToArray();
            var auditLogs = (await _unitOfWork.AuditLogs.ListAsync()).ToArray();
            var monetario = await _monetaryConfigurationService.GetCurrentStateAsync(cancellationToken);
            var faltantes = await _shortageService.GetShortagesAsync(cancellationToken);

            var productosActivos = productos.Where(p => p.Activo).ToArray();
            var insumosActivos = recursos.Where(r => r.Activo && !r.EsServicio).ToArray();
            var productosPorId = productos.ToDictionary(p => p.Id, p => p);

            var ordenesPeriodo = ordenes.Where(o => EnPeriodo(o.FechaFin ?? o.CreatedAt, year, month)).ToArray();
            var costHistoryPeriodo = costHistory.Where(h => EnPeriodo(h.Fecha, year, month)).ToArray();

            var variacionPromedio = CalcularVariacionPromedio(costHistoryPeriodo);

            return new BusinessIntelligenceReport
            {
                PeriodoDescripcion = DescribirPeriodo(year, month),
                ProductosActivos = productosActivos.Length,
                InsumosActivos = insumosActivos.Length,
                CostoPromedioProducto = productosActivos.Length == 0 ? 0m : productosActivos.Average(p => p.CostoFabricacionActual),
                MargenPromedio = productosActivos.Length == 0 ? 0m : productosActivos.Average(p => p.MargenActual),
                ValorInventarioInsumos = insumosActivos.Sum(r => r.StockActual * r.PrecioEnPesos(monetario.CurrentRate)),
                ValorInventarioProductos = productosActivos.Sum(p => p.StockActual * p.CostoFabricacionActual),
                OrdenesFinalizadas = ordenesPeriodo.Count(o => o.Estado == EstadoOrdenProduccion.Finalizada),
                OrdenesEnProceso = ordenes.Count(o => o.Estado == EstadoOrdenProduccion.EnProceso),
                ProduccionValorizada = ordenesPeriodo
                    .Where(o => o.Estado == EstadoOrdenProduccion.Finalizada)
                    .SelectMany(o => o.Items)
                    .Sum(i => (productosPorId.TryGetValue(i.ProductoId, out var p) ? p.PrecioSugeridoActual : 0m) * i.Cantidad),
                VariacionCostoPromedioPct = variacionPromedio,
                CotizacionVigente = monetario.CurrentRate,

                CostoPorProducto = Normalizar(productosActivos
                    .Where(p => p.CostoFabricacionActual > 0)
                    .OrderByDescending(p => p.CostoFabricacionActual)
                    .Take(TopItems)
                    .Select(p => new ChartPoint
                    {
                        Etiqueta = p.Nombre,
                        Valor = p.CostoFabricacionActual,
                        ValorFormateado = FormatoMoneda(p.CostoFabricacionActual),
                        Detalle = $"Precio sugerido {FormatoMoneda(p.PrecioSugeridoActual)}"
                    })),

                MayoresAumentos = Normalizar(CalcularAumentos(costHistoryPeriodo, productosPorId)),

                // Distinct: el enum tiene alias (Pendiente = Borrador, Completada = Finalizada) que, sin él, duplican barras.
                OrdenesPorEstado = Normalizar(Enum.GetValues<EstadoOrdenProduccion>()
                    .Distinct()
                    .Select(estado => new ChartPoint
                    {
                        Etiqueta = EtiquetaEstado(estado),
                        Valor = ordenes.Count(o => o.Estado == estado),
                        ValorFormateado = ordenes.Count(o => o.Estado == estado).ToString(Cultura)
                    })
                    .Where(point => point.Valor > 0)),

                EvolucionCosto = Normalizar(CalcularEvolucion(costHistory)),

                ActividadPorModulo = Normalizar(auditLogs
                    .Where(log => EnPeriodo(log.FechaHora, year, month))
                    .GroupBy(log => string.IsNullOrWhiteSpace(log.Modulo) ? "Sin modulo" : log.Modulo)
                    .OrderByDescending(grupo => grupo.Count())
                    .Take(TopItems)
                    .Select(grupo => new ChartPoint
                    {
                        Etiqueta = grupo.Key,
                        Valor = grupo.Count(),
                        ValorFormateado = grupo.Count().ToString(Cultura),
                        Detalle = $"{grupo.Select(l => l.Usuario).Distinct(StringComparer.OrdinalIgnoreCase).Count()} usuario(s)"
                    })),

                InsumosPorValorInmovilizado = Normalizar(insumosActivos
                    .Select(r => new
                    {
                        Recurso = r,
                        Valor = r.StockActual * r.PrecioEnPesos(monetario.CurrentRate)
                    })
                    .Where(x => x.Valor > 0)
                    .OrderByDescending(x => x.Valor)
                    .Take(TopItems)
                    .Select(x => new ChartPoint
                    {
                        Etiqueta = x.Recurso.Nombre,
                        Valor = x.Valor,
                        ValorFormateado = FormatoMoneda(x.Valor),
                        Detalle = $"{x.Recurso.StockActual:N2} {x.Recurso.UnidadMedida}"
                    })),

                Alertas = ConstruirAlertas(
                    variacionPromedio,
                    faltantes.TotalCount,
                    productosActivos,
                    costHistory,
                    monetario.LastUpdate)
            };
        }

        private static decimal CalcularVariacionPromedio(IReadOnlyCollection<ProductCostHistory> historial)
        {
            var conBase = historial.Where(h => h.CostoAnterior > 0).ToArray();
            if (conBase.Length == 0)
            {
                return 0m;
            }

            return Math.Round(conBase.Average(h => (h.CostoNuevo - h.CostoAnterior) / h.CostoAnterior) * 100m, 2);
        }

        private static IEnumerable<ChartPoint> CalcularAumentos(
            IReadOnlyCollection<ProductCostHistory> historial,
            IReadOnlyDictionary<Guid, Producto> productosPorId)
        {
            return historial
                .Where(h => h.CostoAnterior > 0 && h.CostoNuevo > h.CostoAnterior)
                .GroupBy(h => h.ProductId)
                .Select(grupo =>
                {
                    var ultimo = grupo.OrderByDescending(h => h.Fecha).First();
                    var primero = grupo.OrderBy(h => h.Fecha).First();
                    var variacion = (ultimo.CostoNuevo - primero.CostoAnterior) / primero.CostoAnterior * 100m;
                    var nombre = productosPorId.TryGetValue(grupo.Key, out var producto) ? producto.Nombre : grupo.Key.ToString();
                    return new ChartPoint
                    {
                        Etiqueta = nombre,
                        Valor = Math.Round(variacion, 2),
                        ValorFormateado = $"{Math.Round(variacion, 2).ToString("N2", Cultura)} %",
                        Detalle = $"{FormatoMoneda(primero.CostoAnterior)} → {FormatoMoneda(ultimo.CostoNuevo)}"
                    };
                })
                .OrderByDescending(point => point.Valor)
                .Take(TopItems);
        }

        private static IEnumerable<ChartPoint> CalcularEvolucion(IReadOnlyCollection<ProductCostHistory> historial)
        {
            var cursor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(MesesEvolucion - 1));
            for (var i = 0; i < MesesEvolucion; i++)
            {
                var mes = cursor.AddMonths(i);
                var delMes = historial.Where(h => h.Fecha.Year == mes.Year && h.Fecha.Month == mes.Month).ToArray();
                var promedio = delMes.Length == 0 ? 0m : delMes.Average(h => h.CostoNuevo);
                var etiqueta = mes.ToString("MMM yy", Cultura);

                yield return new ChartPoint
                {
                    Etiqueta = char.ToUpperInvariant(etiqueta[0]) + etiqueta[1..],
                    Valor = Math.Round(promedio, 2),
                    ValorFormateado = FormatoMoneda(promedio),
                    Detalle = $"{delMes.Length} recálculo(s)"
                };
            }
        }

        private static IReadOnlyList<DecisionAlert> ConstruirAlertas(
            decimal variacionCostoPromedioPct,
            int faltantes,
            IReadOnlyCollection<Producto> productosActivos,
            IReadOnlyCollection<ProductCostHistory> costHistory,
            DateTime ultimaCotizacion)
        {
            var alertas = new List<DecisionAlert>();

            if (faltantes > 0)
            {
                alertas.Add(new DecisionAlert
                {
                    Severidad = "Critica",
                    Titulo = $"{faltantes} item(s) por debajo del stock mínimo",
                    Detalle = "Hay productos o insumos cuyo stock actual no alcanza el umbral configurado.",
                    Recomendacion = "Revisar la pantalla de Faltantes y generar las órdenes de compra o producción correspondientes."
                });
            }

            if (variacionCostoPromedioPct >= 10m)
            {
                alertas.Add(new DecisionAlert
                {
                    Severidad = "Critica",
                    Titulo = $"Los costos subieron {variacionCostoPromedioPct.ToString("N2", Cultura)} % en promedio",
                    Detalle = "El promedio de variación de los recálculos del período supera el 10 %.",
                    Recomendacion = "Revisar los precios de venta sugeridos: con el margen actual el aumento de costos se está absorbiendo."
                });
            }

            var sinTrazabilidad = productosActivos
                .Where(p => costHistory.All(h => h.ProductId != p.Id))
                .ToArray();
            if (sinTrazabilidad.Length > 0)
            {
                alertas.Add(new DecisionAlert
                {
                    Severidad = "Advertencia",
                    Titulo = $"{sinTrazabilidad.Length} producto(s) sin historial de costos",
                    Detalle = "Esos productos nunca registraron un recálculo, por lo que su evolución de costo no se puede reconstruir.",
                    Recomendacion = "Ejecutar un recálculo de costos sobre esos productos para dejar la línea de base en el historial."
                });
            }

            var margenBajo = productosActivos.Where(p => p.MargenActual > 0 && p.MargenActual < 0.15m).ToArray();
            if (margenBajo.Length > 0)
            {
                alertas.Add(new DecisionAlert
                {
                    Severidad = "Advertencia",
                    Titulo = $"{margenBajo.Length} producto(s) con margen menor al 15 %",
                    Detalle = string.Join(", ", margenBajo.Take(5).Select(p => p.Nombre)),
                    Recomendacion = "Evaluar si conviene renegociar el precio de los insumos o ajustar el precio de venta."
                });
            }

            if (ultimaCotizacion != default && (DateTime.UtcNow - ultimaCotizacion).TotalDays > 7)
            {
                alertas.Add(new DecisionAlert
                {
                    Severidad = "Advertencia",
                    Titulo = "La cotización del dólar tiene más de 7 días",
                    Detalle = $"Última actualización: {ultimaCotizacion.ToLocalTime():dd/MM/yyyy HH:mm}.",
                    Recomendacion = "Actualizar la cotización antes de recalcular costos de insumos importados."
                });
            }

            if (alertas.Count == 0)
            {
                alertas.Add(new DecisionAlert
                {
                    Severidad = "Ok",
                    Titulo = "Sin alertas para el período",
                    Detalle = "No se detectaron faltantes, aumentos de costo relevantes ni brechas de trazabilidad.",
                    Recomendacion = "Mantener la frecuencia actual de recálculo de costos y actualización de cotización."
                });
            }

            return alertas;
        }

        private static IReadOnlyList<ChartPoint> Normalizar(IEnumerable<ChartPoint> puntos)
        {
            var lista = puntos.ToArray();
            if (lista.Length == 0)
            {
                return lista;
            }

            var maximo = lista.Max(point => Math.Abs(point.Valor));
            foreach (var punto in lista)
            {
                punto.Escala = maximo == 0 ? 0 : (double)(Math.Abs(punto.Valor) / maximo);
            }

            return lista;
        }

        private static bool EnPeriodo(DateTime valor, int? year, int? month)
        {
            if (year is null || month is null)
            {
                return true;
            }

            return valor.Year == year.Value && valor.Month == month.Value;
        }

        private static string DescribirPeriodo(int? year, int? month)
        {
            if (year is null || month is null)
            {
                return "Historico completo";
            }

            var etiqueta = new DateTime(year.Value, month.Value, 1).ToString("MMMM yyyy", Cultura);
            return char.ToUpperInvariant(etiqueta[0]) + etiqueta[1..];
        }

        private static string FormatoMoneda(decimal valor) => valor.ToString("C2", Cultura);
    }
}
