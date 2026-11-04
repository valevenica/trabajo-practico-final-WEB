using System;
using System.Collections.Generic;
using System.Linq;

namespace StockManufactura.Domain.Entities
{
    public sealed class OrdenProduccion : BaseEntity
    {
        private readonly List<OrdenProduccionItem> _items = new();

        public OrdenProduccion(string codigo, IEnumerable<(Guid ProductoId, decimal Cantidad)> items, string observaciones, Guid? clienteId = null)
        {
            Codigo = codigo;
            Observaciones = observaciones;
            ClienteId = clienteId;
            Estado = EstadoOrdenProduccion.Borrador;
            CargarItems(items);
        }

        /// <summary>Orden de un solo producto.</summary>
        public OrdenProduccion(string codigo, Guid productoId, decimal cantidadPlaneada, string observaciones, Guid? clienteId = null)
            : this(codigo, new[] { (productoId, cantidadPlaneada) }, observaciones, clienteId)
        {
        }

        private OrdenProduccion() { }

        public string Codigo { get; private set; } = string.Empty;

        /// <summary>Productos del pedido, cada uno con su cantidad.</summary>
        public IReadOnlyCollection<OrdenProduccionItem> Items => _items.AsReadOnly();

        /// <summary>Cliente del pedido; <c>null</c> cuando la venta es a consumidor final.</summary>
        public Guid? ClienteId { get; private set; }

        /// <summary>Total de unidades pedidas, sumando todos los productos.</summary>
        public decimal CantidadPlaneada => _items.Sum(x => x.Cantidad);
        public decimal CantidadProducida { get; private set; }
        public EstadoOrdenProduccion Estado { get; private set; }
        public string Observaciones { get; private set; } = string.Empty;
        public DateTime? FechaInicio { get; private set; }
        public DateTime? FechaFin { get; private set; }

        /// <summary>Estado que tenía la orden al cancelarse; es al que vuelve si se restaura.</summary>
        public EstadoOrdenProduccion? EstadoAntesDeCancelar { get; private set; }

        /// <summary>
        /// Estado al que vuelve una orden cancelada al restaurarla. Las órdenes canceladas antes de que se guardara ese
        /// dato vuelven a en proceso si ya se habían iniciado, o a borrador si no.
        /// </summary>
        public EstadoOrdenProduccion EstadoParaRestaurar => EstadoAntesDeCancelar
            ?? (FechaInicio.HasValue ? EstadoOrdenProduccion.EnProceso : EstadoOrdenProduccion.Borrador);

        public void AsignarCliente(Guid? clienteId)
        {
            if (Estado == EstadoOrdenProduccion.Finalizada || Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("No se puede cambiar el cliente de una orden finalizada o cancelada.");
            }

            ClienteId = clienteId;
            UpdateTimestamp();
        }

        public void Planificar()
        {
            if (Estado == EstadoOrdenProduccion.Finalizada || Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("No se puede planificar una orden finalizada o cancelada. Una finalizada se reabre y una cancelada se restaura.");
            }

            Estado = EstadoOrdenProduccion.Planificada;
            UpdateTimestamp();
        }

        public void MarcarEnProgreso()
        {
            if (Estado == EstadoOrdenProduccion.Finalizada)
            {
                throw new InvalidOperationException("Una orden finalizada solo puede reabrirse para volver a en proceso.");
            }

            if (Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("Una orden cancelada solo puede restaurarse al estado que tenía antes.");
            }

            Estado = EstadoOrdenProduccion.EnProceso;
            FechaInicio ??= DateTime.UtcNow;
            UpdateTimestamp();
        }

        public void RegistrarProduccion(decimal cantidad)
        {
            if (Estado == EstadoOrdenProduccion.Finalizada)
            {
                throw new InvalidOperationException("No se puede registrar producción en una orden completada.");
            }

            if (Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("No se puede registrar producción en una orden cancelada.");
            }

            if (Estado == EstadoOrdenProduccion.Borrador || Estado == EstadoOrdenProduccion.Planificada)
            {
                MarcarEnProgreso();
            }

            CantidadProducida = cantidad;
            UpdateTimestamp();
        }

        public void Completar()
        {
            if (Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("No se puede completar una orden cancelada.");
            }

            if (Estado == EstadoOrdenProduccion.Borrador || Estado == EstadoOrdenProduccion.Planificada)
            {
                MarcarEnProgreso();
            }

            Estado = EstadoOrdenProduccion.Finalizada;
            FechaFin ??= DateTime.UtcNow;
            UpdateTimestamp();
        }

        public void Cancelar(string motivo)
        {
            if (Estado == EstadoOrdenProduccion.Finalizada)
            {
                throw new InvalidOperationException("No se puede cancelar una orden finalizada.");
            }

            if (Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("La orden ya está cancelada.");
            }

            EstadoAntesDeCancelar = Estado;
            Estado = EstadoOrdenProduccion.Cancelada;
            if (!string.IsNullOrWhiteSpace(motivo))
            {
                Observaciones = string.IsNullOrWhiteSpace(Observaciones)
                    ? motivo.Trim()
                    : $"{Observaciones}{Environment.NewLine}[Cancelada] {motivo.Trim()}";
            }

            FechaFin ??= DateTime.UtcNow;
            UpdateTimestamp();
        }

        public void VolverABorrador()
        {
            if (Estado == EstadoOrdenProduccion.Finalizada)
            {
                throw new InvalidOperationException("No se puede volver a borrador una orden finalizada; solo puede reabrirse para volver a en proceso.");
            }

            if (Estado == EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("Una orden cancelada solo puede restaurarse al estado que tenía antes.");
            }

            Estado = EstadoOrdenProduccion.Borrador;
            FechaInicio = null;
            FechaFin = null;
            UpdateTimestamp();
        }

        /// <summary>
        /// Reabre una orden finalizada: vuelve a en proceso (sin mover stock, que ya estaba descontado) y se borra la fecha
        /// de fin hasta que se vuelva a finalizar.
        /// </summary>
        public void Reabrir()
        {
            if (Estado != EstadoOrdenProduccion.Finalizada)
            {
                throw new InvalidOperationException("Solo se puede reabrir una orden finalizada.");
            }

            Estado = EstadoOrdenProduccion.EnProceso;
            FechaInicio ??= DateTime.UtcNow;
            FechaFin = null;
            UpdateTimestamp();
        }

        /// <summary>Devuelve una orden cancelada al estado que tenía antes de cancelarse.</summary>
        public void RestaurarCancelada()
        {
            if (Estado != EstadoOrdenProduccion.Cancelada)
            {
                throw new InvalidOperationException("Solo se puede restaurar una orden cancelada.");
            }

            var destino = EstadoParaRestaurar;
            Estado = destino;
            EstadoAntesDeCancelar = null;
            FechaFin = null;
            if (destino == EstadoOrdenProduccion.EnProceso)
            {
                FechaInicio ??= DateTime.UtcNow;
            }

            UpdateTimestamp();
        }

        public void ActualizarObservaciones(string observaciones)
        {
            Observaciones = observaciones ?? string.Empty;
            UpdateTimestamp();
        }

        /// <summary>
        /// Reemplaza los productos del pedido. Solo mientras está en espera (borrador o planificada), porque recién al
        /// pasar a en proceso se descuenta el stock de cada producto.
        /// </summary>
        public void ReemplazarItems(IEnumerable<(Guid ProductoId, decimal Cantidad)> items)
        {
            if (Estado != EstadoOrdenProduccion.Borrador && Estado != EstadoOrdenProduccion.Planificada)
            {
                throw new InvalidOperationException("Solo se pueden cambiar los productos de una orden en borrador o planificada.");
            }

            CargarItems(items);
            UpdateTimestamp();
        }

        private void CargarItems(IEnumerable<(Guid ProductoId, decimal Cantidad)> items)
        {
            // El mismo producto cargado dos veces se suma en una sola línea.
            var agrupados = (items ?? Enumerable.Empty<(Guid ProductoId, decimal Cantidad)>())
                .GroupBy(x => x.ProductoId)
                .Select(g => new OrdenProduccionItem(Id, g.Key, g.Sum(x => x.Cantidad)))
                .ToList();

            if (agrupados.Count == 0)
            {
                throw new InvalidOperationException("La orden debe tener al menos un producto.");
            }

            _items.Clear();
            _items.AddRange(agrupados);
        }

        /// <summary>
        /// Efecto de un cambio de estado sobre el stock del producto: -1 descuenta las unidades planeadas, +1 las reintegra
        /// y 0 no mueve stock. Las órdenes en espera (borrador o planificada) solo reservan; el descuento se aplica al salir
        /// de la espera hacia en proceso o finalizada, y se revierte cuando una orden en proceso vuelve a la espera o se cancela.
        /// </summary>
        public static int MovimientoDeStock(EstadoOrdenProduccion anterior, EstadoOrdenProduccion nuevo)
        {
            var enEspera = (EstadoOrdenProduccion estado) =>
                estado == EstadoOrdenProduccion.Borrador || estado == EstadoOrdenProduccion.Planificada;

            if (enEspera(anterior) && (nuevo == EstadoOrdenProduccion.EnProceso || nuevo == EstadoOrdenProduccion.Finalizada))
            {
                return -1;
            }

            // Una orden cancelada que se restaura a en proceso vuelve a descontar lo que se reintegró al cancelarla.
            if (anterior == EstadoOrdenProduccion.Cancelada && nuevo == EstadoOrdenProduccion.EnProceso)
            {
                return -1;
            }

            if (anterior == EstadoOrdenProduccion.EnProceso && (enEspera(nuevo) || nuevo == EstadoOrdenProduccion.Cancelada))
            {
                return 1;
            }

            return 0;
        }
    }

    public enum EstadoOrdenProduccion
    {
        Borrador = 1,
        Planificada = 2,
        EnProceso = 3,
        Finalizada = 4,
        Cancelada = 5,
        Pendiente = Borrador,
        Completada = Finalizada
    }
}
