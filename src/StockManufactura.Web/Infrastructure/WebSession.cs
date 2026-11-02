using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;

namespace StockManufactura.Web.Infrastructure
{
    /// <summary>
    /// Equivalente web de AuthSession. La cookie solo guarda el id del usuario; el usuario y su rol se leen de la base
    /// una vez por circuito, así un cambio de permisos o de contraseña se refleja sin volver a iniciar sesión.
    /// </summary>
    public sealed class WebSession
    {
        public const string Equipo = "Web";

        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private Usuario? _usuario;
        private bool _loaded;

        public WebSession(AuthenticationStateProvider authenticationStateProvider, IServiceScopeFactory scopeFactory)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _scopeFactory = scopeFactory;
        }

        public Usuario? Usuario => _usuario;

        /// <summary>Identificación usada en la auditoría (el correo del usuario).</summary>
        public string Actor => _usuario?.Email ?? "web-user";

        public async Task<Usuario?> GetUsuarioAsync()
        {
            if (_loaded)
            {
                return _usuario;
            }

            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var id = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(id, out var userId))
            {
                using var scope = _scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var usuario = await unitOfWork.Usuarios.GetByIdWithRoleAsync(userId);
                _usuario = usuario is { EsActivo: true } ? usuario : null;
            }

            _loaded = true;
            return _usuario;
        }

        public bool TienePermiso(string permiso)
        {
            // Solo el rol decide los permisos.
            var roleName = _usuario?.Rol?.Nombre?.Trim();
            if (string.Equals(roleName, DatabaseSeeder.AdminRoleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return _usuario?.Rol?.TienePermiso(permiso) == true;
        }

        public bool PasswordChangePending => _usuario?.RequiereCambioPassword == true;

        public bool CanEnter(string modulo)
        {
            if (_usuario is null)
            {
                return false;
            }

            if (PasswordChangePending)
            {
                return modulo == ModuleAccess.Dashboard;
            }

            return ModuleAccess.Permisos(modulo) is not { } permisos || permisos.Any(TienePermiso);
        }
    }
}
