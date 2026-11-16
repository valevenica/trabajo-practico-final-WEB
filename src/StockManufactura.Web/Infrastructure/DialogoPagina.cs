namespace StockManufactura.Web.Infrastructure
{
    /// <summary>Pedido de confirmación mostrado dentro de la página (reemplaza confirm/prompt del navegador).</summary>
    public sealed record DialogoSolicitud(string Mensaje, string TextoAceptar, bool Peligroso, string? FraseRequerida);

    /// <summary>
    /// Cuadro de diálogo propio de la página, uno por circuito. Las pantallas lo abren desde <c>PaginaBase</c> y lo dibuja
    /// el componente <c>Dialogo</c> que incluye <c>PaginaMarco</c>; así los avisos se ven con el estilo del sistema y no
    /// como ventanas emergentes del navegador.
    /// </summary>
    public sealed class DialogoPagina
    {
        private TaskCompletionSource<bool>? _respuesta;

        public DialogoSolicitud? Actual { get; private set; }

        public event Action? Cambio;

        public Task<bool> MostrarAsync(DialogoSolicitud solicitud)
        {
            // Un diálogo nuevo cancela el anterior que hubiera quedado abierto.
            Responder(false);
            Actual = solicitud;
            _respuesta = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Cambio?.Invoke();
            return _respuesta.Task;
        }

        public void Responder(bool aceptado)
        {
            var pendiente = _respuesta;
            _respuesta = null;
            Actual = null;
            if (pendiente is not null)
            {
                pendiente.TrySetResult(aceptado);
                Cambio?.Invoke();
            }
        }
    }
}
