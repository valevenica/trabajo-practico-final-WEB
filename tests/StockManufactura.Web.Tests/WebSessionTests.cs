using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using StockManufactura.Application.Interfaces;
using StockManufactura.Domain.Entities;
using StockManufactura.UnitTests.TestDoubles;
using StockManufactura.Web.Infrastructure;
using Xunit;

namespace StockManufactura.Web.Tests;

/// <summary>
/// La sesión web decide qué opciones del menú ve cada usuario y si puede entrar a cada pantalla, con las mismas reglas
/// que el resto del sistema.
/// </summary>
public class WebSessionTests
{
    private const string RolAdministrador = "Administrador";

    private readonly FakeUnitOfWork _unitOfWork = new();

    private Usuario CrearUsuario(string rolNombre, params string[] permisos)
    {
        var rol = new Rol(rolNombre, "Rol de prueba");
        rol.AsignarPermisos(permisos);
        var usuario = new Usuario("usuario", "usuario@test.com", "hash", rol.Id);
        usuario.AsignarRol(rol);
        _unitOfWork.RolesFake.Seed(rol);
        _unitOfWork.UsuariosFake.Seed(usuario);
        return usuario;
    }

    private async Task<WebSession> SesionDe(Guid? usuarioId)
    {
        var services = new ServiceCollection().AddSingleton<IUnitOfWork>(_unitOfWork).BuildServiceProvider();
        var session = new WebSession(new EstadoFijo(usuarioId), services.GetRequiredService<IServiceScopeFactory>());
        await session.GetUsuarioAsync();
        return session;
    }

    [Fact]
    public async Task SinCookie_NoHayUsuarioNiAcceso()
    {
        var session = await SesionDe(null);

        Assert.Null(session.Usuario);
        Assert.False(session.CanEnter(ModuleAccess.Dashboard));
        Assert.Equal("web-user", session.Actor);
    }

    [Fact]
    public async Task Administrador_EntraATodosLosModulosAunqueElRolNoListePermisos()
    {
        var admin = CrearUsuario(RolAdministrador);

        var session = await SesionDe(admin.Id);

        Assert.All(ModuleAccessTests.ModulosProtegidos(), m => Assert.True(session.CanEnter((string)m[0])));
        Assert.Equal("usuario@test.com", session.Actor);
    }

    [Fact]
    public async Task RolRestringido_SoloVeLosModulosDeSusPermisos()
    {
        var operario = CrearUsuario("Solo stock", "STOCK_VER");

        var session = await SesionDe(operario.Id);

        Assert.True(session.CanEnter(ModuleAccess.Dashboard));
        Assert.True(session.CanEnter(ModuleAccess.Shortages));
        Assert.False(session.CanEnter(ModuleAccess.Products));
        Assert.False(session.CanEnter(ModuleAccess.Backups));
        Assert.False(session.CanEnter(ModuleAccess.Users));
    }

    [Fact]
    public async Task CambioDePasswordPendiente_SoloPermiteElPanel()
    {
        var usuario = CrearUsuario(RolAdministrador);
        usuario.ForzarCambioPassword();

        var session = await SesionDe(usuario.Id);

        Assert.True(session.PasswordChangePending);
        Assert.True(session.CanEnter(ModuleAccess.Dashboard));
        Assert.False(session.CanEnter(ModuleAccess.Products));
    }

    [Fact]
    public async Task UsuarioDesactivado_PierdeLaSesion()
    {
        var usuario = CrearUsuario(RolAdministrador);
        usuario.Desactivar();

        var session = await SesionDe(usuario.Id);

        Assert.Null(session.Usuario);
        Assert.False(session.CanEnter(ModuleAccess.Dashboard));
    }

    private sealed class EstadoFijo : AuthenticationStateProvider
    {
        private readonly Guid? _usuarioId;

        public EstadoFijo(Guid? usuarioId) => _usuarioId = usuarioId;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = _usuarioId is { } id
                ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "Prueba")
                : new ClaimsIdentity();
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
