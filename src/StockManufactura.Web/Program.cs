using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using StockManufactura.Application.Interfaces;
using StockManufactura.Web.Components;
using StockManufactura.Web.Infrastructure;

// Formatos de Argentina (moneda, fechas, decimales) aunque el servidor tenga otro idioma, como un contenedor Linux.
var cultura = CultureInfo.GetCultureInfo("es-AR");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var builder = WebApplication.CreateBuilder(args);

// Con "dotnet run" fuera de Development los archivos de Blazor (_framework/blazor.web.js, estilos aislados) no se
// sirven si no se habilita esto; en la salida publicada no tiene efecto.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "StockManufactura.Auth";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddStockManufacturaWeb(builder.Configuration);

// Detrás del proxy de un hosting (por ejemplo Render), el esquema y la IP originales llegan en encabezados X-Forwarded-*.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

RegistroSerilog.Configurar(app.Services.GetRequiredService<ILoggerFactory>());
app.Services.InitializeDatabase();
if (DemoSetup.Habilitada(app.Configuration))
{
    DemoSetup.Preparar(app.Services);
}

app.UseForwardedHeaders();
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(cultura.Name)
    .AddSupportedCultures(cultura.Name)
    .AddSupportedUICultures(cultura.Name));

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapPost("/cuenta/salir", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
});

// Descarga de copias de seguridad: un enlace directo evita mandar archivos grandes por la conexión de la página.
app.MapGet("/backups/{id:guid}/descargar", async (Guid id, HttpContext context, IUnitOfWork unitOfWork) =>
{
    if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        || await unitOfWork.Usuarios.GetByIdWithRoleAsync(userId) is not { EsActivo: true } usuario
        || usuario.Rol?.TienePermiso("USUARIOS_ADMIN") != true)
    {
        // Forbid() redirigiría a una página de acceso denegado inexistente; se responde 403 directamente.
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var registro = (await unitOfWork.BackupRecords.ListAsync()).FirstOrDefault(x => x.Id == id);
    return registro is not null && File.Exists(registro.RutaArchivo)
        ? Results.File(registro.RutaArchivo, "application/zip", Path.GetFileName(registro.RutaArchivo))
        : Results.NotFound();
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Expone la clase generada por las instrucciones de nivel superior para las pruebas de integración (WebApplicationFactory).
public partial class Program;
