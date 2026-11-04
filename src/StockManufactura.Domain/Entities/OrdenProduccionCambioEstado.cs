using System;
using System.Collections.Generic;
using System.Linq;

namespace StockManufactura.Domain.Entities
{
    /// <summary>
    /// Registro de un cambio de estado de una orden: de qué estado a cuál pasó, cuándo y quién lo hizo. El alta de la
    /// orden se guarda con <see cref="EstadoAnterior"/> en <c>null</c>.
    /// </summary>
    public sealed class OrdenProduccionCambioEstado : BaseEntity
    {
        public OrdenProduccionCambioEstado(Guid ordenProduccionId, EstadoOrdenProduccion? estadoAnterior, EstadoOrdenProduccion estadoNuevo, string usuario, DateTime? fecha = null)
        {
            OrdenProduccionId = ordenProduccionId;
            EstadoAnterior = estadoAnterior;
            EstadoNuevo = estadoNuevo;
            Usuario = usuario ?? string.Empty;
            Fecha = fecha ?? DateTime.UtcNow;
        }

        private OrdenProduccionCambioEstado() { }

        public Guid OrdenProduccionId { get; private set; }
        public EstadoOrdenProduccion? EstadoAnterior { get; private set; }
        public EstadoOrdenProduccion EstadoNuevo { get; private set; }

        /// <summary>Momento del cambio, en UTC.</summary>
        public DateTime Fecha { get; private set; }
        public string Usuario { get; private set; } = string.Empty;

        /// <summary>
        /// Tiempo de armado de una orden finalizada: la suma de los tramos en que estuvo en proceso, según su historial.
        /// Devuelve <c>null</c> si la orden no está finalizada o si nunca pasó un tiempo medible en proceso (por ejemplo,
        /// porque se finalizó directamente con stock disponible).
        /// </summary>
        public static TimeSpan? TiempoDeArmado(IEnumerable<OrdenProduccionCambioEstado> historial)
        {
            var cambios = (historial ?? Enumerable.Empty<OrdenProduccionCambioEstado>()).OrderBy(x => x.Fecha).ToList();
            if (cambios.Count == 0 || cambios[^1].EstadoNuevo != EstadoOrdenProduccion.Finalizada)
            {
                return null;
            }

            var total = TimeSpan.Zero;
            DateTime? inicioTramo = null;
            foreach (var cambio in cambios)
            {
                if (cambio.EstadoNuevo == EstadoOrdenProduccion.EnProceso)
                {
                    inicioTramo ??= cambio.Fecha;
                }
                else if (inicioTramo is { } inicio)
                {
                    total += cambio.Fecha - inicio;
                    inicioTramo = null;
                }
            }

            return total > TimeSpan.Zero ? total : null;
        }
    }
}
