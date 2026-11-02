using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using StockManufactura.Web.Infrastructure;

namespace StockManufactura.Web.Components
{
    public static class RenderModes
    {
        /// <summary>Interactivo en el servidor sin prerenderizado, para no consultar la base dos veces por visita.</summary>
        public static readonly InteractiveServerRenderMode Interactivo = new(prerender: false);
    }

    /// <summary>
    /// Base de las pantallas de gestión. Cada página resuelve sus servicios (y por lo tanto su DbContext) desde un ámbito
    /// propio que se libera al salir de ella: un contexto de datos por pantalla.
    /// </summary>
    public abstract class PaginaBase : OwningComponentBase
    {
        [Inject] protected WebSession Session { get; set; } = default!;
        [Inject] protected NavigationManager Navigation { get; set; } = default!;
        [Inject] protected IJSRuntime JS { get; set; } = default!;

        protected abstract string Modulo { get; }

        protected bool Cargando { get; private set; } = true;
        protected string? AccesoDenegado { get; private set; }
        protected string StatusMessage { get; set; } = string.Empty;
        protected bool Ocupado { get; private set; }

        private readonly Dictionary<Type, object> _servicios = new();

        /// <summary>
        /// Una instancia por tipo durante la vida de la página: los servicios son transitorios, y resolverlos en cada uso
        /// daría un DbContext distinto entre la consulta y el guardado.
        /// </summary>
        protected T Servicio<T>() where T : notnull
        {
            if (!_servicios.TryGetValue(typeof(T), out var servicio))
            {
                servicio = ScopedServices.GetRequiredService<T>();
                _servicios[typeof(T)] = servicio;
            }

            return (T)servicio;
        }

        protected override async Task OnInitializedAsync()
        {
            var usuario = await Session.GetUsuarioAsync();
            if (usuario is null)
            {
                Navigation.NavigateTo($"login?returnUrl={Uri.EscapeDataString(Navigation.ToBaseRelativePath(Navigation.Uri))}", forceLoad: true);
                return;
            }

            if (!Session.CanEnter(Modulo))
            {
                AccesoDenegado = Session.PasswordChangePending
                    ? "Debe cambiar su contraseña antes de usar el resto del sistema."
                    : $"No tiene permisos para acceder a {ModuleAccess.Nombre(Modulo)}. Solicite al administrador que asigne el permiso correspondiente a su rol.";
                Cargando = false;
                return;
            }

            try
            {
                await CargarAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error al cargar: {ex.Message}";
            }
            finally
            {
                Cargando = false;
            }
        }

        protected abstract Task CargarAsync();

        /// <summary>
        /// Ejecuta una acción del usuario evitando operaciones simultáneas sobre el mismo DbContext (doble clic) y
        /// mostrando el error en la barra de estado en lugar de cortar el circuito.
        /// </summary>
        protected async Task EjecutarAsync(Func<Task> accion)
        {
            if (Ocupado)
            {
                return;
            }

            Ocupado = true;
            try
            {
                await accion();
            }
            catch (Exception ex)
            {
                StatusMessage = ex.InnerException?.Message ?? ex.Message;
            }
            finally
            {
                Ocupado = false;
            }
        }

        protected ValueTask<bool> ConfirmarAsync(string mensaje) => JS.InvokeAsync<bool>("confirm", mensaje);

        /// <summary>Entrega un archivo generado en el servidor como descarga del navegador (ver wwwroot/js/app.js).</summary>
        protected ValueTask Descargar(string nombre, string contenido, string tipo)
            => JS.InvokeVoidAsync("stockManufactura.descargar", nombre, contenido, tipo);

        protected ValueTask Descargar(string nombre, byte[] contenido, string tipo)
            => JS.InvokeVoidAsync("stockManufactura.descargarBytes", nombre, contenido, tipo);
    }
}
