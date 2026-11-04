using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Historial de estados de las órdenes: cada alta y cada cambio de estado queda con su fecha y usuario. Lo usan la
    /// pantalla de órdenes y el panel principal; el registro se guarda junto con el cambio, en el mismo SaveChanges.
    /// </summary>
    public static class OrdenHistorial
    {
        public static Task RegistrarAsync(IUnitOfWork unitOfWork, OrdenProduccion orden, EstadoOrdenProduccion? anterior, string usuario)
        {
            if (anterior == orden.Estado)
            {
                return Task.CompletedTask;
            }

            return unitOfWork.CambiosEstadoOrden.AddAsync(new OrdenProduccionCambioEstado(orden.Id, anterior, orden.Estado, usuario));
        }

        /// <summary>
        /// Tiempo de armado de una orden finalizada. Usa el historial; para las órdenes finalizadas antes de que existiera,
        /// toma la diferencia entre la fecha de inicio y la de fin.
        /// </summary>
        public static TimeSpan? TiempoDeArmado(OrdenProduccion orden, IReadOnlyCollection<OrdenProduccionCambioEstado> historial)
        {
            if (orden.Estado != EstadoOrdenProduccion.Finalizada)
            {
                return null;
            }

            if (historial.Count > 0)
            {
                return OrdenProduccionCambioEstado.TiempoDeArmado(historial);
            }

            return orden.FechaInicio is { } inicio && orden.FechaFin is { } fin && fin > inicio ? fin - inicio : null;
        }

        public static string FormatearDuracion(TimeSpan duracion)
        {
            if (duracion.TotalDays >= 1) { return $"{(int)duracion.TotalDays} d {duracion.Hours} h"; }
            if (duracion.TotalHours >= 1) { return $"{(int)duracion.TotalHours} h {duracion.Minutes} min"; }
            return $"{Math.Max(1, (int)Math.Round(duracion.TotalMinutes))} min";
        }
    }
}
