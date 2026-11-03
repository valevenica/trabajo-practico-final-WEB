using Microsoft.Playwright;

// Pruebas de punta a punta de la versión web, en Chrome, contra un servidor ya levantado.
// ATENCIÓN: modifican datos (desactivan y reactivan registros, crean usuarios, restauran una copia). Usar SOLO con una
// base de prueba. Ver README.md de esta carpeta.
var B = (Environment.GetEnvironmentVariable("E2E_URL") ?? "http://localhost:5199/").TrimEnd('/') + "/";
if (args.Length > 0 && args[0] == "casos")
{
    return await CasosFuncionales.EjecutarAsync(B);
}

var usuarioAdmin = Environment.GetEnvironmentVariable("E2E_USUARIO") ?? "Admin";
var passwordAdmin = Environment.GetEnvironmentVariable("E2E_PASSWORD") ?? "Admin12345";
var shots = Path.Combine(AppContext.BaseDirectory, "capturas");
Directory.CreateDirectory(shots);
var sufijo = DateTime.Now.ToString("HHmmss");

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true });
var ctx = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1366, Height = 900 }, AcceptDownloads = true });
var page = await ctx.NewPageAsync();
page.SetDefaultTimeout(10000);
string? respuestaPrompt = null;
page.Dialog += async (_, d) => { if (d.Type == "prompt") await d.AcceptAsync(respuestaPrompt ?? ""); else await d.AcceptAsync(); };
page.Console += (_, m) => { if (m.Type == "error") Console.WriteLine($"  [error de consola] {m.Text}"); };

int correctas = 0, fallas = 0;
async Task Check(string nombre, Func<Task> paso)
{
    try { await paso(); correctas++; Console.WriteLine($"OK   {nombre}"); }
    catch (Exception ex)
    {
        fallas++;
        Console.WriteLine($"FAIL {nombre}: {ex.Message.Split('\n')[0]}");
        var archivo = string.Concat(nombre.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        await page.ScreenshotAsync(new() { Path = Path.Combine(shots, $"falla-{archivo}.png"), FullPage = true });
    }
}

async Task SinError()
{
    if (await page.Locator("#blazor-error-ui").IsVisibleAsync()) throw new Exception("se mostró el aviso de error de Blazor");
    var st = page.Locator(".status");
    if (await st.CountAsync() > 0 && (await st.Last.InnerTextAsync()).StartsWith("Error al cargar")) throw new Exception(await st.Last.InnerTextAsync());
}

async Task<string> Status() => (await page.Locator(".status").Last.InnerTextAsync()).Trim();
async Task Ir(string texto) { await page.Locator(".nav a", new() { HasTextString = texto }).First.ClickAsync(); await page.WaitForTimeoutAsync(900); await SinError(); }
async Task<List<string>> OpcionesComponentes() => (await page.Locator(".inline-form select option").AllInnerTextsAsync()).ToList();
async Task SeleccionarProducto(string nombre) { await page.Locator(".item-list li", new() { HasTextString = nombre }).First.ClickAsync(); await page.WaitForTimeoutAsync(800); }

// ---------------------------------------------------------------- Ingreso
await Check("ingreso: ver contraseña y recuperar", async () =>
{
    await page.GotoAsync(B + "login");
    await page.FillAsync("#password", "secreto");
    await page.ClickAsync(".btn-eye");
    if (await page.GetAttributeAsync("#password", "type") != "text") throw new Exception("el botón no muestra la contraseña");
    await page.ClickAsync("text=¿Olvidó su contraseña?");
    await page.WaitForURLAsync("**/cuenta/recuperar");
    await page.Locator("#correo").WaitForAsync(); await page.WaitForTimeoutAsync(600);
    await page.FillAsync("#correo", "nadie@ejemplo.com");
    await page.ClickAsync("text=Enviar código"); await page.WaitForTimeoutAsync(1500);
    // No revela si el correo existe.
    if (!(await page.Locator(".alert").InnerTextAsync()).StartsWith("Si el correo corresponde")) throw new Exception("respuesta inesperada");
    await page.ClickAsync("text=Volver al ingreso"); await page.WaitForURLAsync("**/login");
});

await Check("ingreso: contraseña incorrecta y correcta", async () =>
{
    await page.FillAsync("#usuario", usuarioAdmin); await page.FillAsync("#password", "incorrecta");
    await page.ClickAsync("button[type=submit]"); await page.Locator(".alert-error").WaitForAsync();
    await page.FillAsync("#usuario", usuarioAdmin); await page.FillAsync("#password", passwordAdmin);
    await page.ClickAsync("button[type=submit]"); await page.Locator(".nav").WaitForAsync();
    if (page.Url.Contains("cuenta/password")) throw new Exception("el administrador tiene el cambio de contraseña pendiente; cámbiela antes de correr la suite");
});

// ---------------------------------------------------------------- Menú y módulos
await Check("menú agrupado por área", async () =>
{
    var grupos = (await page.Locator(".nav-group").AllInnerTextsAsync()).Select(x => x.Trim().ToUpperInvariant()).ToList();
    var esperado = new[] { "DASHBOARD", "STOCK", "PROVEEDORES", "CLIENTES", "PRODUCCIÓN", "FINANZAS", "ADMINISTRACIÓN" };
    if (!grupos.SequenceEqual(esperado)) throw new Exception(string.Join(" | ", grupos));
});

var modulos = new[] { "Panel principal", "Inteligencia de negocio", "Faltantes", "Productos", "Insumos", "Insumos especiales", "Ingresos",
    "Carga masiva de precios", "Proveedores", "Clientes", "Órdenes", "Historial de costos", "Configuración monetaria", "Usuarios", "Backups", "Bitácora" };
foreach (var op in modulos)
    await Check($"abrir {op}", async () => { await Ir(op); await page.Locator("h1").WaitForAsync(); });

// ---------------------------------------------------------------- Panel e inteligencia de negocio
await Check("panel: período, filtro por estado y búsqueda", async () =>
{
    await page.GotoAsync(B); await page.Locator(".kpi").First.WaitForAsync();
    await page.Locator(".period-picker select").SelectOptionAsync("todo"); await page.WaitForTimeoutAsync(800);
    await page.ClickAsync(".multi-select summary");
    await page.Locator(".multi-select-panel label", new() { HasText = "Cancelada" }).ClickAsync();
    await page.FillAsync(".filters input:not([type=checkbox])", "zzz-no-existe");
    await page.Locator("td", new() { HasText = "No hay pedidos" }).WaitForAsync();
    await page.FillAsync(".filters input:not([type=checkbox])", "");
    await SinError();
});

await Check("inteligencia de negocio: indicadores y textos con tildes", async () =>
{
    await page.GotoAsync(B + "inteligencia"); await page.Locator(".kpi").First.WaitForAsync(); await page.WaitForTimeoutAsync(800);
    var texto = await page.Locator("main").InnerTextAsync();
    foreach (var sinTilde in new[] { "stock minimo", "recalculo", "evolucion de costo", "linea de base" })
        if (texto.Contains(sinTilde)) throw new Exception($"texto sin tilde: «{sinTilde}»");
    await SinError();
});

// ---------------------------------------------------------------- Productos e insumos (desactivar en lugar de eliminar)
await Check("productos: desactivar, reactivar y sin botón Eliminar", async () =>
{
    await page.GotoAsync(B + "productos");
    var items = page.Locator(".item-list li:not(.muted)");
    await items.First.WaitForAsync();
    var total = await items.CountAsync();
    var nombre = (await items.First.Locator(".item-title").InnerTextAsync()).Trim();
    await items.First.ClickAsync();
    if (await page.GetByRole(AriaRole.Button, new() { Name = "Eliminar" }).CountAsync() != 0) throw new Exception("existe el botón Eliminar");
    await page.GetByRole(AriaRole.Button, new() { Name = "Desactivar", Exact = true }).ClickAsync();
    await page.Locator(".status").WaitForAsync();
    if (await items.CountAsync() != total - 1) throw new Exception("el producto desactivado sigue en el listado");
    await page.Locator("label.check", new() { HasText = "Mostrar inactivos" }).ClickAsync();
    await page.Locator(".item-list li.inactive", new() { HasText = nombre }).ClickAsync();
    await page.GetByRole(AriaRole.Button, new() { Name = "Reactivar", Exact = true }).ClickAsync();
    await page.Locator(".status", new() { HasText = "reactivado" }).WaitForAsync();
    await page.Locator("label.check", new() { HasText = "Mostrar inactivos" }).ClickAsync();
    await SinError();
});

await Check("productos: margen visible y guardar", async () =>
{
    await page.GotoAsync(B + "productos");
    await page.Locator(".item-list li:not(.muted)").First.ClickAsync(); await page.WaitForTimeoutAsync(700);
    var margen = page.Locator(".form-grid div", new() { HasText = "Margen" }).Locator("input");
    if (string.IsNullOrEmpty(await margen.InputValueAsync())) throw new Exception("el campo Margen aparece vacío");
    await page.GetByRole(AriaRole.Button, new() { Name = "Guardar", Exact = true }).ClickAsync();
    await page.Locator(".status", new() { HasText = "Producto actualizado" }).WaitForAsync();
});

await Check("F34 producto usado como componente y desactivado", async () =>
{
    await page.GotoAsync(B + "productos"); await page.WaitForTimeoutAsync(1200);
    var nombres = (await page.Locator(".item-list li .item-title").AllInnerTextsAsync()).Select(x => x.Trim()).ToList();
    if (nombres.Count < 2) { Console.WriteLine("  (se necesitan dos productos activos; se omite)"); return; }
    var a = nombres[0]; var b = nombres[1];
    await SeleccionarProducto(b);
    if (await page.Locator("tbody tr td:first-child", new() { HasTextString = a }).CountAsync() == 0)
    {
        var opcion = (await OpcionesComponentes()).First(o => o.StartsWith($"[Producto] {a}"));
        await page.SelectOptionAsync(".inline-form select", new SelectOptionValue { Label = opcion });
        await page.Locator(".inline-form input[type=number]").FillAsync("1");
        await page.ClickAsync(".inline-form button:has-text('+ Agregar')"); await page.WaitForTimeoutAsync(1200);
    }
    await SeleccionarProducto(a);
    await page.GetByRole(AriaRole.Button, new() { Name = "Desactivar", Exact = true }).ClickAsync(); await page.WaitForTimeoutAsync(1000);
    await SeleccionarProducto(b);
    if (await page.Locator("tbody tr td:first-child", new() { HasTextString = a }).CountAsync() == 0) throw new Exception("desapareció de la receta existente");
    if ((await OpcionesComponentes()).Any(o => o.StartsWith($"[Producto] {a}"))) throw new Exception("se sigue ofreciendo como componente");
    await page.Locator("label.check", new() { HasText = "Mostrar inactivos" }).ClickAsync();
    await page.Locator(".item-list li.inactive", new() { HasTextString = a }).ClickAsync(); await page.WaitForTimeoutAsync(600);
    await page.GetByRole(AriaRole.Button, new() { Name = "Reactivar", Exact = true }).ClickAsync(); await page.WaitForTimeoutAsync(1000);
    await SeleccionarProducto(b);
    if (!(await OpcionesComponentes()).Any(o => o.StartsWith($"[Producto] {a}"))) throw new Exception("no volvió a ofrecerse tras reactivar");
});

await Check("F35 insumo desactivado no se ofrece en recetas", async () =>
{
    await page.GotoAsync(B + "insumos"); await page.WaitForTimeoutAsync(1200);
    await page.Locator(".item-list li").First.ClickAsync(); await page.WaitForTimeoutAsync(800);
    var nombre = await page.Locator(".form-grid input").Nth(1).InputValueAsync();
    await page.GetByRole(AriaRole.Button, new() { Name = "Desactivar insumo" }).ClickAsync(); await page.WaitForTimeoutAsync(1000);
    await page.GotoAsync(B + "productos"); await page.WaitForTimeoutAsync(1200);
    await page.Locator(".item-list li").First.ClickAsync(); await page.WaitForTimeoutAsync(800);
    if ((await OpcionesComponentes()).Any(o => o.StartsWith(nombre + " ("))) throw new Exception("el insumo inactivo se ofrece en recetas");
    await page.GotoAsync(B + "insumos"); await page.WaitForTimeoutAsync(1200);
    await page.Locator("label.check", new() { HasText = "Mostrar inactivos" }).ClickAsync(); await page.WaitForTimeoutAsync(400);
    await page.Locator(".item-list li", new() { HasTextString = nombre }).First.ClickAsync(); await page.WaitForTimeoutAsync(800);
    await page.GetByRole(AriaRole.Button, new() { Name = "Reactivar insumo" }).ClickAsync(); await page.WaitForTimeoutAsync(1000);
    await page.GotoAsync(B + "productos"); await page.WaitForTimeoutAsync(1200);
    await page.Locator(".item-list li").First.ClickAsync(); await page.WaitForTimeoutAsync(800);
    if (!(await OpcionesComponentes()).Any(o => o.StartsWith(nombre + " ("))) throw new Exception("no volvió a ofrecerse");
});

await Check("insumos especiales: tramos y simulación", async () =>
{
    await page.GotoAsync(B + "insumos/especiales");
    await page.GetByRole(AriaRole.Button, new() { Name = "+ Nuevo insumo especial" }).ClickAsync();
    var inputs = page.Locator(".card", new() { HasText = "Nuevo insumo especial" }).Locator("input");
    var codigo = "LUZ-" + sufijo;
    await inputs.Nth(0).FillAsync(codigo);
    await inputs.Nth(1).FillAsync("Electricidad " + codigo);
    await inputs.Nth(2).FillAsync("kWh");
    await page.GetByRole(AriaRole.Button, new() { Name = "Crear", Exact = true }).ClickAsync();
    await page.Locator(".status", new() { HasText = "Insumo especial creado" }).WaitForAsync();
    var filas = page.Locator("tbody tr").Filter(new() { Has = page.Locator("input") });
    await filas.Nth(0).Locator("input").Nth(0).FillAsync("100");
    await filas.Nth(0).Locator("input").Nth(1).FillAsync("10");
    await page.GetByRole(AriaRole.Button, new() { Name = "+ Agregar tramo" }).ClickAsync();
    await filas.Nth(1).Locator("input").Nth(1).FillAsync("15");
    await page.GetByRole(AriaRole.Button, new() { Name = "Guardar tramos" }).ClickAsync();
    await page.Locator(".status", new() { HasText = "Tramos guardados" }).WaitForAsync();
    await page.Locator("input[placeholder^='Consumo']").FillAsync("150");
    var texto = await page.Locator("p", new() { HasText = "Costo total" }).InnerTextAsync();
    if (!texto.Contains("1750")) throw new Exception($"esperaba $1750 (100×10 + 50×15): {texto}");
});

await Check("ingreso de stock", async () =>
{
    await page.GotoAsync(B + "stock/ingresos");
    // El desplegable de la carga manual; el otro es el del proveedor de la factura.
    var select = page.Locator(".inline-form select");
    await select.WaitForAsync();
    var valor = await select.Locator("optgroup[label=Insumos] option").First.GetAttributeAsync("value");
    await select.SelectOptionAsync(valor!);
    await page.Locator(".inline-form input[type=number]").FillAsync("5");
    await page.GetByRole(AriaRole.Button, new() { Name = "+ Agregar a la lista" }).ClickAsync();
    await page.GetByRole(AriaRole.Button, new() { Name = "Registrar ingreso" }).ClickAsync();
    await page.Locator(".status", new() { HasText = "registrado correctamente" }).WaitForAsync();
});

await Check("órdenes: crear y planificar", async () =>
{
    await page.GotoAsync(B + "ordenes");
    await page.Locator(".kpi").First.WaitForAsync();
    await page.Locator(".inline-form input[type=number]").FillAsync("1");
    await page.GetByRole(AriaRole.Button, new() { Name = "+ Agregar producto" }).ClickAsync();
    await page.GetByRole(AriaRole.Button, new() { Name = "Crear orden" }).ClickAsync();
    await page.Locator(".status").WaitForAsync();
    await page.GetByRole(AriaRole.Button, new() { Name = "Planificar" }).ClickAsync();
    await page.Locator(".status", new() { HasText = "planificada" }).WaitForAsync();
    await SinError();
});

// ---------------------------------------------------------------- Finanzas
await Check("cotización manual y prioritaria", async () =>
{
    await page.GotoAsync(B + "cotizacion"); await page.WaitForTimeoutAsync(900);
    await page.Locator(".form-grid input[type=number]").FillAsync("1234.5");
    await page.Locator(".form-grid input").Nth(1).FillAsync("Prueba e2e");
    await page.ClickAsync("text=Guardar manual"); await page.WaitForTimeoutAsync(800);
    if (!(await Status()).StartsWith("Cotización manual guardada")) throw new Exception(await Status());
    // Se elige una cotización que no sea la activa (la activa tiene la estrella en la primera columna).
    var inactivas = page.Locator("tbody tr").Filter(new() { Has = page.Locator("td:first-child:not(:has-text('★'))") });
    if (await inactivas.CountAsync() > 0)
    {
        await inactivas.First.ClickAsync(); await page.ClickAsync("text=★ Usar esta cotización"); await page.WaitForTimeoutAsync(800);
        if (!(await Status()).Contains("activa", StringComparison.OrdinalIgnoreCase) && !(await Status()).Contains("prioritaria", StringComparison.OrdinalIgnoreCase)) throw new Exception(await Status());
    }
    await SinError();
});

await Check("carga masiva de precios con vista previa", async () =>
{
    await page.GotoAsync(B + "insumos/precios"); await page.WaitForTimeoutAsync(900);
    var proveedores = page.Locator("select option:not([value=''])");
    await page.SelectOptionAsync("select", (await proveedores.First.GetAttributeAsync("value"))!);
    await page.WaitForTimeoutAsync(400);
    await page.SetInputFilesAsync("input[type=file]", Path.Combine(AppContext.BaseDirectory, "precios.xlsx"));
    await page.Locator("h2:has-text('Vista previa')").WaitForAsync(); await page.WaitForTimeoutAsync(500);
    Console.WriteLine($"  {await Status()}");
    await SinError();
});

await Check("historial de costos desde productos", async () =>
{
    await page.GotoAsync(B + "productos"); await page.WaitForTimeoutAsync(900);
    await page.Locator(".item-list li").First.ClickAsync(); await page.WaitForTimeoutAsync(500);
    await page.ClickAsync("text=Ver historial de costos");
    await page.WaitForURLAsync("**/productos/historial?producto=*"); await page.WaitForTimeoutAsync(900); await SinError();
});

// ---------------------------------------------------------------- Administración
await Check("bitácora: filtrar ingresos y exportar CSV", async () =>
{
    await page.GotoAsync(B + "bitacora"); await page.WaitForTimeoutAsync(900);
    await page.Locator(".filters input").Nth(1).FillAsync("Autenticacion");
    await page.ClickAsync("button:has-text('Buscar')"); await page.WaitForTimeoutAsync(800);
    var acciones = (await page.Locator("tbody tr td:nth-child(4)").AllInnerTextsAsync()).Distinct().ToList();
    if (!acciones.Contains("LoginFallido") || !acciones.Contains("LoginExitoso")) throw new Exception("faltan los ingresos de esta prueba: " + string.Join(", ", acciones));
    var dl = await page.RunAndWaitForDownloadAsync(() => page.ClickAsync("text=Exportar CSV"));
    if (!dl.SuggestedFilename.EndsWith(".csv")) throw new Exception(dl.SuggestedFilename);
});

await Check("backups: generar, descargar y restaurar con confirmación", async () =>
{
    await page.GotoAsync(B + "backups"); await page.WaitForTimeoutAsync(900);
    await page.ClickAsync("text=Generar copia ahora"); await page.WaitForTimeoutAsync(2000);
    var dl = await page.RunAndWaitForDownloadAsync(() => page.Locator("a:has-text('Descargar')").First.ClickAsync());
    if (!dl.SuggestedFilename.EndsWith(".zip")) throw new Exception(dl.SuggestedFilename);
    respuestaPrompt = "no";
    await page.Locator("button:has-text('Restaurar')").First.ClickAsync(); await page.WaitForTimeoutAsync(800);
    if (await Status() != "Restauración cancelada.") throw new Exception(await Status());
    respuestaPrompt = "RESTAURAR";
    await page.Locator("button:has-text('Restaurar')").First.ClickAsync(); await page.WaitForTimeoutAsync(3000);
    if (!(await Status()).StartsWith("Restauración completada")) throw new Exception(await Status());
    await SinError();
});

await Check("usuarios: alta, contraseña temporal, rol y permisos", async () =>
{
    await page.GotoAsync(B + "usuarios"); await page.WaitForTimeoutAsync(1200);
    await page.ClickAsync("button:has-text('+ Nuevo')");
    var inputs = page.Locator("section.card:not(.list-panel) .form-grid input");
    await inputs.Nth(0).FillAsync("operario" + sufijo); await inputs.Nth(1).FillAsync($"operario{sufijo}@e2e.test");
    await page.Locator("section.card:not(.list-panel) input[type=password]").FillAsync("Operario123");
    await page.ClickAsync("section.card:not(.list-panel) button:has-text('Guardar')"); await page.WaitForTimeoutAsync(1000);
    await page.Locator(".inline-form input[type=password]").FillAsync("Temporal123");
    await page.ClickAsync("text=Asignar contraseña temporal"); await page.WaitForTimeoutAsync(1000);
    if (!(await Status()).StartsWith("Contraseña temporal asignada")) throw new Exception(await Status());
    await page.ClickAsync(".tab:has-text('Correo de recuperación')"); await page.WaitForTimeoutAsync(300); await SinError();
});

await Check("operario con contraseña temporal: cambio obligatorio", async () =>
{
    var p2 = await (await browser.NewContextAsync()).NewPageAsync();
    await p2.GotoAsync(B + "login");
    await p2.FillAsync("#usuario", "operario" + sufijo); await p2.FillAsync("#password", "Temporal123");
    await p2.ClickAsync("button[type=submit]"); await p2.WaitForURLAsync("**/cuenta/password");
    await p2.GotoAsync(B + "backups"); await p2.WaitForTimeoutAsync(1200);
    if (!(await p2.Locator(".alert").First.InnerTextAsync()).Contains("Debe cambiar su contraseña")) throw new Exception("no exigió el cambio");
});

await Check("F38 rol restringido: menú, acceso y descarga", async () =>
{
    await page.GotoAsync(B + "usuarios"); await page.WaitForTimeoutAsync(1200);
    await page.ClickAsync(".tab:has-text('Roles y permisos')");
    await page.ClickAsync(".list-panel button:has-text('+ Nuevo')");
    await page.Locator(".sub-card input").FillAsync("Solo stock " + sufijo);
    await page.ClickAsync("text=Crear rol"); await page.WaitForTimeoutAsync(1000);
    await page.Locator(".permisos label", new() { HasText = "Ver stock" }).ClickAsync();
    await page.ClickAsync("text=Guardar permisos"); await page.WaitForTimeoutAsync(800);
    await page.ClickAsync(".tab:has-text('Usuarios')"); await page.WaitForTimeoutAsync(300);
    await page.ClickAsync("button:has-text('+ Nuevo')");
    var inputs = page.Locator("section.card:not(.list-panel) .form-grid input");
    await inputs.Nth(0).FillAsync("stock" + sufijo); await inputs.Nth(1).FillAsync($"stock{sufijo}@e2e.test");
    await page.SelectOptionAsync("section.card:not(.list-panel) select", new SelectOptionValue { Label = "Solo stock " + sufijo });
    await page.Locator("section.card:not(.list-panel) input[type=password]").FillAsync("Stock12345");
    await page.ClickAsync("section.card:not(.list-panel) button:has-text('Guardar')"); await page.WaitForTimeoutAsync(1000);

    var p2 = await (await browser.NewContextAsync()).NewPageAsync();
    await p2.GotoAsync(B + "login");
    await p2.FillAsync("#usuario", "stock" + sufijo); await p2.FillAsync("#password", "Stock12345");
    await p2.ClickAsync("button[type=submit]"); await p2.WaitForURLAsync("**/cuenta/password");
    await p2.FillAsync("#actual", "Stock12345"); await p2.FillAsync("#nueva", "StockNueva123"); await p2.FillAsync("#confirmacion", "StockNueva123");
    await p2.ClickAsync("button:has-text('Guardar contraseña')"); await p2.WaitForTimeoutAsync(2000);
    await p2.GotoAsync(B); await p2.WaitForTimeoutAsync(1200);
    var menu = await p2.Locator(".nav a").AllInnerTextsAsync();
    if (menu.Any(m => m.Contains("Backups") || m.Contains("Usuarios") || m.Contains("Productos"))) throw new Exception("muestra módulos no habilitados: " + string.Join(", ", menu));
    await p2.GotoAsync(B + "backups"); await p2.WaitForTimeoutAsync(1200);
    if (!(await p2.Locator(".alert").First.InnerTextAsync()).StartsWith("No tiene permisos")) throw new Exception("no se rechazó el acceso");
    var r = await p2.APIRequest.GetAsync(B + "backups/00000000-0000-0000-0000-000000000000/descargar");
    if (r.Status != 403) throw new Exception($"la descarga respondió HTTP {r.Status}");
});

await Check("mi cuenta", async () =>
{
    await page.GotoAsync(B + "cuenta"); await page.WaitForTimeoutAsync(900);
    await page.ClickAsync("text=Guardar mis datos"); await page.WaitForTimeoutAsync(800);
    if (!(await Status()).StartsWith("Sus datos fueron actualizados")) throw new Exception(await Status());
});

// ---------------------------------------------------------------- Celular
await Check("celular: sin desplazamiento horizontal en ninguna pantalla", async () =>
{
    await page.SetViewportSizeAsync(390, 844);
    var desbordes = new List<string>();
    foreach (var ruta in new[] { "", "inteligencia", "faltantes", "productos", "insumos", "insumos/especiales", "stock/ingresos", "insumos/precios",
        "proveedores", "clientes", "ordenes", "productos/historial", "cotizacion", "usuarios", "backups", "bitacora", "cuenta", "cuenta/password" })
    {
        await page.GotoAsync(B + ruta); await page.WaitForTimeoutAsync(1000);
        var lista = page.Locator(".item-list li");
        if (await lista.CountAsync() > 0) { await lista.First.ClickAsync(); await page.WaitForTimeoutAsync(600); }
        var ancho = await page.EvaluateAsync<int>("document.documentElement.scrollWidth");
        if (ancho > 392) desbordes.Add($"/{ruta} ({ancho}px)");
    }
    if (desbordes.Count > 0) throw new Exception(string.Join(", ", desbordes));
});

await Check("celular: menú plegable", async () =>
{
    await page.GotoAsync(B); await page.WaitForTimeoutAsync(900);
    if (await page.Locator(".nav").IsVisibleAsync()) throw new Exception("el menú debería empezar plegado");
    await page.ClickAsync(".nav-toggle-btn");
    await page.ScreenshotAsync(new() { Path = Path.Combine(shots, "menu-celular.png") });
    await page.ClickAsync(".nav a:has-text('Productos')"); await page.WaitForURLAsync("**/productos"); await page.WaitForTimeoutAsync(600);
    if (await page.Locator(".nav").IsVisibleAsync()) throw new Exception("el menú no se cerró al elegir una opción");
});

await Check("cerrar sesión", async () =>
{
    if (await page.Locator(".nav-toggle-btn").IsVisibleAsync()) await page.ClickAsync(".nav-toggle-btn");
    await page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).ClickAsync();
    await page.WaitForURLAsync("**/login");
});

Console.WriteLine(fallas == 0 ? $"TODO OK ({correctas} controles)" : $"{fallas} FALLA(S) de {correctas + fallas}");
Console.WriteLine($"Capturas en {shots}");
return fallas == 0 ? 0 : 1;
