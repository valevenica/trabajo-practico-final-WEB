using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StockManufactura.Application.Interfaces;
using StockManufactura.Application.Resources;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Application.Services
{
    public sealed class ShortageService : IShortageService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ShortageService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<ShortageReport> GetShortagesAsync(CancellationToken cancellationToken = default)
        {
            var products = await _unitOfWork.Productos.ListActivosAsync();
            var orders = await _unitOfWork.OrdenesProduccion.ListAsync();
            var resources = await _unitOfWork.Recursos.ListActivosAsync();

            var reservedByProduct = orders
                .Where(o => o.Estado == EstadoOrdenProduccion.Borrador || o.Estado == EstadoOrdenProduccion.Planificada)
                .SelectMany(o => o.Items)
                .GroupBy(i => i.ProductoId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Cantidad));

            var productShortages = new List<ProductShortageRow>();
            var orderShortfallByProduct = new Dictionary<Guid, decimal>();
            foreach (var product in products)
            {
                reservedByProduct.TryGetValue(product.Id, out var reserved);

                // Si no hay stock y tampoco hay un minimo configurado, se usa 1 como objetivo minimo
                // implicito para que el producto igual aparezca como faltante (stock en 0 siempre es faltante).
                var minimoObjetivo = product.StockMinimo > 0
                    ? product.StockMinimo
                    : (product.StockActual <= 0 ? 1m : 0m);

                var ordersShortfall = reserved - product.StockActual;
                var minimoShortfall = minimoObjetivo - product.StockActual;

                var faltante = Math.Max(ordersShortfall, minimoShortfall);
                if (faltante <= 0)
                {
                    continue;
                }

                var motivos = new List<string>();
                if (ordersShortfall > 0)
                {
                    motivos.Add("Insuficiente para completar órdenes en espera");
                    orderShortfallByProduct[product.Id] = ordersShortfall;
                }

                if (product.StockActual <= 0)
                {
                    motivos.Add("Sin stock");
                }
                else if (minimoShortfall > 0)
                {
                    motivos.Add("Por debajo del stock mínimo");
                }

                productShortages.Add(new ProductShortageRow
                {
                    ProductoId = product.Id,
                    ProductoNombre = product.Nombre,
                    StockActual = product.StockActual,
                    Reservado = reserved,
                    Faltante = faltante,
                    Motivo = string.Join(" / ", motivos)
                });
            }

            var neededByResource = new Dictionary<Guid, decimal>();
            foreach (var (productoId, ordersShortfall) in orderShortfallByProduct)
            {
                var recipeItems = await _unitOfWork.RecetaProductoItems.ListByProductIdAsync(productoId);
                foreach (var item in recipeItems.Where(x => x.RecursoId.HasValue))
                {
                    var qty = item.Cantidad * ordersShortfall;
                    neededByResource[item.RecursoId!.Value] = neededByResource.GetValueOrDefault(item.RecursoId!.Value) + qty;
                }
            }

            var resourceShortages = new List<ResourceShortageRow>();
            foreach (var resource in resources.Where(r => !r.EsServicio))
            {
                if (neededByResource.TryGetValue(resource.Id, out var needed) && needed > resource.StockActual)
                {
                    resourceShortages.Add(new ResourceShortageRow
                    {
                        RecursoId = resource.Id,
                        RecursoNombre = resource.Nombre,
                        UnidadMedida = resource.UnidadMedida,
                        StockActual = resource.StockActual,
                        Faltante = needed - resource.StockActual,
                        Motivo = "Insuficiente para completar órdenes en espera"
                    });
                    continue;
                }

                if (resource.StockActual < resource.StockMinimo)
                {
                    resourceShortages.Add(new ResourceShortageRow
                    {
                        RecursoId = resource.Id,
                        RecursoNombre = resource.Nombre,
                        UnidadMedida = resource.UnidadMedida,
                        StockActual = resource.StockActual,
                        Faltante = resource.StockMinimo - resource.StockActual,
                        Motivo = "Por debajo del stock mínimo"
                    });
                }
            }

            return new ShortageReport
            {
                Productos = productShortages,
                Insumos = resourceShortages
            };
        }
    }
}
