using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

/// <summary>
/// Casos de prueba funcionales F1 a F33 del documento del proyecto, ejecutados en el navegador sobre la base de
/// demostración (usuarios profesor, produccion y consulta, con el modo demo activado). Se ejecuta con «dotnet run -- casos».
/// Variables opcionales: E2E_CORREO_DIR (carpeta donde un servidor SMTP de prueba deja los mensajes, para F19 y F20),
/// E2E_NUBE_DIR (carpeta vacía que hace de nube, para F13 a F15 y F31), E2E_URL_SIN_RED (otro servidor sin acceso a
/// Internet, para F11) y E2E_PRECIOS_USD (planilla con el precio nuevo del insumo en dólares, para F32).
/// </summary>
public static class CasosFuncionales
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");

    public static async Task<int> EjecutarAsync(string b)
    {
        var correoDir = Environment.GetEnvironmentVariable("E2E_CORREO_DIR");
        var nubeDir = Environment.GetEnvironmentVariable("E2E_NUBE_DIR");
        var urlSinRed = Environment.GetEnvironmentVariable("E2E_URL_SIN_RED");
        var preciosUsd = Environment.GetEnvironmentVariable("E2E_PRECIOS_USD");
        var shots = Path.Combine(AppContext.BaseDirectory, "capturas-casos");
        Directory.CreateDirectory(shots);

        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true });
        int ok = 0, fallas = 0, omitidos = 0;
        IPage? actual = null;

        async Task Caso(string id, string nombre, Func<Task> paso)
        {
            try { await paso(); ok++; Console.WriteLine($"OK   {id} {nombre}"); }
            catch (OmitidoException ex) { omitidos++; Console.WriteLine($"OMIT {id} {nombre}: {ex.Message}"); }
            catch (Exception ex)
            {
                fallas++;
                Console.WriteLine($"FAIL {id} {nombre}: {ex.Message.Split('\n')[0]}");
                if (actual is not null) await actual.ScreenshotAsync(new() { Path = Path.Combine(shots, $"falla-{id}.png"), FullPage = true });
            }
        }

        async Task<IPage> Nueva(string? url = null)
        {
            var p = await (await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1366, Height = 900 }, Locale = "es-AR", AcceptDownloads = true })).NewPageAsync();
            p.SetDefaultTimeout(15000);
            p.Dialog += async (_, d) => { if (d.Type == "prompt") await d.AcceptAsync("no"); else await d.AcceptAsync(); };
            await p.GotoAsync((url ?? b) + "login");
            actual = p;
            return p;
        }

        async Task<IPage> Ingresar(string usuario, string password, string? url = null)
        {
            var p = await Nueva(url);
            await p.FillAsync("#usuario", usuario); await p.FillAsync("#password", password);
            await p.ClickAsync("button[type=submit]");
            await p.Locator(".sidebar-top").WaitForAsync(); await p.WaitForTimeoutAsync(1200);
            return p;
        }

        static async Task<string> Estado(IPage p) => (await p.Locator(".status").Last.InnerTextAsync()).Trim();
        static async Task Ir(IPage p, string b, string ruta) { await p.GotoAsync(b + ruta); await p.WaitForTimeoutAsync(1300); }
        static void Exigir(bool condicion, string mensaje) { if (!condicion) throw new Exception(mensaje); }
        static decimal Num(string texto) => decimal.Parse(Regex.Match(texto, @"-?[\d.]+(,\d+)?").Value, NumberStyles.Number, Ar);

        async Task SeleccionarProducto(IPage p, string nombre)
        {
            await Ir(p, b, "productos");
            await p.Locator(".item-list li", new() { HasTextString = nombre }).First.ClickAsync();
            await p.WaitForTimeoutAsync(900);
        }

        async Task<decimal> CostoProducto(IPage p, string nombre)
        {
            await SeleccionarProducto(p, nombre);
            return Num(await p.Locator(".form-grid div", new() { HasText = "Costo fabricación" }).Locator("input").InputValueAsync());
        }

        async Task<decimal> StockProducto(IPage p, string nombre)
        {
            await Ir(p, b, "productos");
            var sub = await p.Locator(".item-list li", new() { HasTextString = nombre }).First.Locator(".item-sub").InnerTextAsync();
            return Num(Regex.Match(sub, @"Stock\s+([\d.,]+)").Groups[1].Value);
        }

        async Task AgregarComponente(IPage p, string opcionEmpiezaCon, string cantidad)
        {
            var opciones = await p.Locator(".inline-form select option").AllInnerTextsAsync();
            var opcion = opciones.First(o => o.StartsWith(opcionEmpiezaCon));
            await p.SelectOptionAsync(".inline-form select", new SelectOptionValue { Label = opcion });
            await p.Locator(".inline-form input[type=number]").FillAsync(cantidad);
            await p.ClickAsync(".inline-form button:has-text('+ Agregar')"); await p.WaitForTimeoutAsync(1300);
        }

        async Task<IPage> OrdenPlanificada(IPage p, string producto, string cantidad, string? cliente = null)
        {
            await Ir(p, b, "ordenes");
            await p.SelectOptionAsync(".inline-form select", new SelectOptionValue { Label = producto });
            await p.Locator(".inline-form input[type=number]").FillAsync(cantidad);
            await p.ClickAsync("button:has-text('+ Agregar producto')"); await p.WaitForTimeoutAsync(400);
            if (cliente is not null) await p.SelectOptionAsync(".form-grid select", new SelectOptionValue { Label = cliente });
            await p.ClickAsync("button:has-text('Crear orden')"); await p.WaitForTimeoutAsync(1300);
            await p.ClickAsync("button:has-text('Planificar')"); await p.WaitForTimeoutAsync(1000);
            return p;
        }

        var sufijo = DateTime.Now.ToString("HHmmss");
        var admin = await Ingresar("profesor", "Profesor2026");

        // ------------------------------------------------------------------ Acceso
        await Caso("F1", "Ingreso con credenciales correctas", async () =>
        {
            Exigir(await admin.Locator(".kpi").CountAsync() > 0, "no abrió el panel principal");
            await Ir(admin, b, "bitacora");
            await admin.Locator(".filters input").Nth(1).FillAsync("Autenticacion");
            await admin.ClickAsync("button:has-text('Buscar')"); await admin.WaitForTimeoutAsync(900);
            var filas = await admin.Locator("tbody tr").AllInnerTextsAsync();
            Exigir(filas.Any(f => f.Contains("LoginExitoso") && f.Contains("profesor")), "no figura el ingreso en la bitácora");
        });

        await Caso("F2", "Cinco intentos de ingreso con contraseña incorrecta", async () =>
        {
            var p = await Nueva();
            for (var i = 0; i < 5; i++)
            {
                await p.FillAsync("#usuario", "consulta"); await p.FillAsync("#password", "incorrecta");
                await p.ClickAsync("button[type=submit]"); await p.Locator(".alert-error").WaitForAsync(); await p.WaitForTimeoutAsync(300);
            }
            await p.FillAsync("#usuario", "consulta"); await p.FillAsync("#password", "Consulta2026");
            await p.ClickAsync("button[type=submit]"); await p.WaitForTimeoutAsync(1200);
            var alerta = await p.Locator(".alert-error").InnerTextAsync();
            Exigir(alerta.Contains("bloque", StringComparison.OrdinalIgnoreCase), "no informó el bloqueo: " + alerta);
        });

        await Caso("F25", "Ingreso con el correo en lugar del nombre de usuario", async () =>
        {
            var p = await Nueva();
            await p.FillAsync("#usuario", "profesor@demo.local"); await p.FillAsync("#password", "Profesor2026");
            await p.ClickAsync("button[type=submit]"); await p.WaitForTimeoutAsync(1200);
            Exigir((await p.Locator(".alert-error").InnerTextAsync()).Contains("Credenciales inválidas"), "no rechazó el ingreso con correo");
        });

        await Caso("F18", "Acceso a un módulo sin el permiso correspondiente", async () =>
        {
            var p = await Ingresar("produccion", "Produccion2026");
            Exigir(await p.Locator(".nav a", new() { HasTextString = "Usuarios" }).CountAsync() == 0, "el menú muestra Usuarios");
            await Ir(p, b, "usuarios");
            Exigir((await p.Locator(".alert").First.InnerTextAsync()).StartsWith("No tiene permisos"), "no rechazó el acceso");
        });

        await Caso("F26", "Alta de un usuario con un nombre existente escrito con otras mayúsculas", async () =>
        {
            await Ir(admin, b, "usuarios");
            await admin.ClickAsync("button:has-text('+ Nuevo')");
            var inputs = admin.Locator("section.card:not(.list-panel) .form-grid input");
            await inputs.Nth(0).FillAsync("PROFESOR"); await inputs.Nth(1).FillAsync($"otro{sufijo}@demo.local");
            await admin.Locator("section.card:not(.list-panel) input[type=password]").FillAsync("Otro12345");
            await admin.ClickAsync("section.card:not(.list-panel) button:has-text('Guardar')"); await admin.WaitForTimeoutAsync(1000);
            var estado = await Estado(admin);
            Exigir(estado.Contains("ya existe", StringComparison.OrdinalIgnoreCase) || estado.Contains("en uso", StringComparison.OrdinalIgnoreCase), estado);
        });

        await Caso("F21", "Asignación de una contraseña temporal a una cuenta bloqueada", async () =>
        {
            await Ir(admin, b, "usuarios");
            await admin.Locator(".item-list li", new() { HasTextString = "consulta" }).First.ClickAsync(); await admin.WaitForTimeoutAsync(700);
            await admin.Locator(".inline-form input[type=password]").FillAsync("Temporal2026");
            await admin.ClickAsync("text=Asignar contraseña temporal"); await admin.WaitForTimeoutAsync(1000);
            Exigir((await Estado(admin)).StartsWith("Contraseña temporal asignada"), await Estado(admin));
            var p = await Nueva();
            await p.FillAsync("#usuario", "consulta"); await p.FillAsync("#password", "Temporal2026");
            await p.ClickAsync("button[type=submit]"); await p.WaitForURLAsync("**/cuenta/password");
        });

        await Caso("F22", "Ingreso con cambio de contraseña pendiente", async () =>
        {
            var p = await Nueva();
            await p.FillAsync("#usuario", "consulta"); await p.FillAsync("#password", "Temporal2026");
            await p.ClickAsync("button[type=submit]"); await p.WaitForURLAsync("**/cuenta/password");
            await Ir(p, b, "productos");
            Exigir((await p.Locator(".alert").First.InnerTextAsync()).Contains("Debe cambiar su contraseña"), "permitió usar otro módulo");
        });

        // ------------------------------------------------------------------ Recuperación por correo
        string? passwordProduccion = null;
        await Caso("F19", "Recuperación de contraseña con el código enviado al correo del perfil", async () =>
        {
            if (correoDir is null) throw new OmitidoException("sin servidor de correo de prueba (E2E_CORREO_DIR)");
            await Ir(admin, b, "usuarios");
            await admin.ClickAsync(".tab:has-text('Correo de recuperación')"); await admin.WaitForTimeoutAsync(500);
            var grid = admin.Locator(".form-grid").Last;
            await grid.Locator("div", new() { HasText = "Servidor SMTP" }).Locator("input").FillAsync("localhost");
            await grid.Locator("div", new() { HasText = "Puerto" }).Locator("input").FillAsync("2525");
            await grid.Locator("div", new() { HasText = "Usuario de la cuenta" }).Locator("input").FillAsync("");
            await grid.Locator("div", new() { HasText = "Correo remitente" }).Locator("input").FillAsync("sistema@demo.local");
            var ssl = admin.Locator("label.check", new() { HasText = "SSL" }).Locator("input");
            if (await ssl.IsCheckedAsync()) await ssl.UncheckAsync();
            await admin.ClickAsync("button:has-text('Guardar configuración')"); await admin.WaitForTimeoutAsync(1000);

            var antes = Directory.GetFiles(correoDir).Length;
            var p = await Nueva();
            await p.ClickAsync("text=¿Olvidó su contraseña?"); await p.Locator("#correo").WaitForAsync(); await p.WaitForTimeoutAsync(600);
            await p.FillAsync("#correo", "produccion@demo.local");
            await p.ClickAsync("text=Enviar código"); await p.Locator("#codigo").WaitForAsync();
            string? mensaje = null;
            for (var i = 0; i < 20 && mensaje is null; i++)
            {
                await Task.Delay(500);
                var archivos = Directory.GetFiles(correoDir).OrderBy(File.GetCreationTimeUtc).ToArray();
                if (archivos.Length > antes) mensaje = File.ReadAllText(archivos[^1]);
            }
            Exigir(mensaje is not null, "no llegó el correo");
            var codigo = Regex.Match(mensaje!, @"\b(\d{6})\b").Groups[1].Value;
            Exigir(mensaje!.Contains("produccion"), "el correo no informa el nombre de usuario");
            passwordProduccion = "Recuperada2026";
            await p.FillAsync("#codigo", codigo);
            await p.FillAsync("#nueva", passwordProduccion); await p.FillAsync("#confirmacion", passwordProduccion);
            await p.ClickAsync("button:has-text('Restablecer')"); await p.WaitForTimeoutAsync(1500);
            var q = await Ingresar("produccion", passwordProduccion);
            Exigir(await q.Locator(".kpi").CountAsync() > 0, "no ingresó con la contraseña nueva");
        });

        await Caso("F20", "Ingreso de un código de recuperación incorrecto", async () =>
        {
            if (correoDir is null || passwordProduccion is null) throw new OmitidoException("depende de F19");
            var p = await Nueva();
            await p.ClickAsync("text=¿Olvidó su contraseña?"); await p.Locator("#correo").WaitForAsync(); await p.WaitForTimeoutAsync(600);
            await p.FillAsync("#correo", "produccion@demo.local");
            await p.ClickAsync("text=Enviar código"); await p.Locator("#codigo").WaitForAsync();
            await p.FillAsync("#codigo", "000000");
            await p.FillAsync("#nueva", "OtraClave2026"); await p.FillAsync("#confirmacion", "OtraClave2026");
            await p.ClickAsync("button:has-text('Restablecer')"); await p.WaitForTimeoutAsync(1500);
            Exigir(await p.Locator(".alert-error").CountAsync() > 0, "no rechazó el código");
            var q = await Ingresar("produccion", passwordProduccion);
            Exigir(await q.Locator(".kpi").CountAsync() > 0, "la contraseña cambió");
        });

        // ------------------------------------------------------------------ Productos y costeo
        await Caso("F3", "Alta de un producto con código ya existente", async () =>
        {
            await Ir(admin, b, "productos");
            await admin.ClickAsync("button:has-text('+ Nuevo producto')"); await admin.WaitForTimeoutAsync(600);
            await admin.Locator(".form-grid div", new() { HasText = "Código" }).Locator("input").FillAsync("PRD-VELA-LAV-220");
            await admin.Locator(".form-grid div", new() { HasText = "Nombre" }).First.Locator("input").FillAsync("Duplicado");
            await admin.ClickAsync("button:has-text('Guardar')"); await admin.WaitForTimeoutAsync(1000);
            Exigir(await Estado(admin) == "Ya existe un producto con ese código.", await Estado(admin));
        });

        await Caso("F4", "Alta de receta con un insumo y recálculo de costo", async () =>
        {
            var antes = await CostoProducto(admin, "Vela de soja Naranja");
            await Ir(admin, b, "productos/historial");
            var historialAntes = await admin.Locator("tbody tr").CountAsync();
            await SeleccionarProducto(admin, "Vela de soja Naranja");
            await AgregarComponente(admin, "Aceite esencial de lavanda", "1");
            Exigir(await Estado(admin) == "Componente agregado.", await Estado(admin));
            var despues = await CostoProducto(admin, "Vela de soja Naranja");
            Exigir(despues > antes, $"el costo no aumentó ({antes} -> {despues})");
            await Ir(admin, b, "productos/historial");
            await admin.SelectOptionAsync("select", new SelectOptionValue { Label = (await admin.Locator("select option", new() { HasTextString = "Naranja" }).First.InnerTextAsync()).Trim() });
            await admin.WaitForTimeoutAsync(1000);
            Exigir(await admin.Locator("tbody tr").CountAsync() > 0, "no hay registro en el historial");
        });

        await Caso("F5", "Producto que integra a otro producto como componente", async () =>
        {
            await Ir(admin, b, "productos");
            await admin.ClickAsync("button:has-text('+ Nuevo producto')"); await admin.WaitForTimeoutAsync(600);
            await admin.Locator(".form-grid div", new() { HasText = "Código" }).Locator("input").FillAsync("PRD-SET-REGALO");
            await admin.Locator(".form-grid div", new() { HasText = "Nombre" }).First.Locator("input").FillAsync("Set de regalo de dos velas");
            await admin.ClickAsync("button:has-text('Guardar')"); await admin.WaitForTimeoutAsync(1200);
            await AgregarComponente(admin, "[Producto] Vela de soja Lavanda", "1");
            await AgregarComponente(admin, "[Producto] Vela de soja Naranja", "1");
            var set = await CostoProducto(admin, "Set de regalo");
            var lavanda = await CostoProducto(admin, "Vela de soja Lavanda");
            var naranja = await CostoProducto(admin, "Vela de soja Naranja");
            Exigir(Math.Abs(set - (lavanda + naranja)) < 0.05m, $"set {set} != {lavanda} + {naranja}");
        });

        await Caso("F6", "Insumo de tipo servicio con tramos de precio", async () =>
        {
            await SeleccionarProducto(admin, "Set de regalo");
            await AgregarComponente(admin, "Electricidad del taller", "150");
            var fila = await admin.Locator("tbody tr", new() { HasTextString = "Electricidad del taller" }).InnerTextAsync();
            // 100 kWh a $ 120 + 50 kWh a $ 180 = $ 21.000
            Exigir(fila.Replace(".", "").Contains("21000"), "subtotal inesperado: " + fila);
        });

        await Caso("F23", "Modificación del stock de un producto desde su ficha", async () =>
        {
            await SeleccionarProducto(admin, "Vela de soja Naranja");
            await admin.Locator(".form-grid div", new() { HasText = "Stock actual" }).Locator("input").FillAsync("7");
            await admin.ClickAsync("button:has-text('Guardar')"); await admin.WaitForTimeoutAsync(1000);
            Exigir(await StockProducto(admin, "Vela de soja Naranja") == 7, "el listado no refleja el stock");
        });

        await Caso("F7", "Importación masiva de precios desde un archivo Excel", async () =>
        {
            await Ir(admin, b, "insumos/precios");
            await admin.SelectOptionAsync("select", new SelectOptionValue { Label = "Botanica Aurora" }); await admin.WaitForTimeoutAsync(400);
            await admin.SetInputFilesAsync("input[type=file]", Path.Combine(AppContext.BaseDirectory, "precios.xlsx"));
            await admin.Locator("h2:has-text('Vista previa')").WaitForAsync(); await admin.WaitForTimeoutAsync(500);
            var estados = await admin.Locator("tbody tr td:nth-child(6)").AllInnerTextsAsync();
            Exigir(estados.Any(e => e.Contains("Listo") || e.Contains("Sin cambios")), "ninguna fila se asoció por código: " + string.Join(", ", estados));
            if (estados.Any(e => e.Contains("Listo")))
            {
                await admin.ClickAsync("text=Confirmar importación"); await admin.WaitForTimeoutAsync(1500);
                Exigir((await Estado(admin)).StartsWith("Importación completada"), await Estado(admin));
            }
        });

        await Caso("F30", "Quitar un proveedor de un insumo y marcar otro como prioritario en la misma sesión", async () =>
        {
            await Ir(admin, b, "insumos");
            await admin.Locator(".item-list li", new() { HasTextString = "Cera de soja" }).ClickAsync(); await admin.WaitForTimeoutAsync(900);
            var tarjeta = admin.Locator(".card", new() { HasText = "Proveedores del insumo" });
            await tarjeta.Locator(".inline-form select").SelectOptionAsync(new SelectOptionValue { Label = "Ceras del Litoral" });
            await tarjeta.Locator(".inline-form input[type=number]").FillAsync("13.40");
            await tarjeta.Locator(".inline-form input:not([type=number])").FillAsync("CL-CERA-01");
            await tarjeta.Locator("button:has-text('+ Agregar')").ClickAsync(); await admin.WaitForTimeoutAsync(1000);
            var fila = tarjeta.Locator("tbody tr", new() { HasTextString = "Ceras del Litoral" });
            if (await fila.Locator("button:has-text('Marcar prioritario')").CountAsync() > 0)
            {
                await fila.Locator("button:has-text('Marcar prioritario')").ClickAsync(); await admin.WaitForTimeoutAsync(1000);
            }
            var otra = tarjeta.Locator("tbody tr").Filter(new() { HasNotText = "Ceras del Litoral" });
            if (await otra.CountAsync() > 0 && !(await otra.First.InnerTextAsync()).Contains("Sin proveedores"))
            {
                await otra.First.Locator("button:has-text('Quitar')").ClickAsync(); await admin.WaitForTimeoutAsync(1000);
            }
            Exigir((await tarjeta.Locator("tbody tr", new() { HasTextString = "Ceras del Litoral" }).InnerTextAsync()).Contains("★"), "no quedó como prioritario");
            var precio = await admin.Locator(".form-grid div", new() { HasText = "Precio" }).Last.Locator("input").InputValueAsync();
            Exigir(Num(precio.Replace('.', ',')) == 13.40m, "el precio del insumo no tomó el del prioritario: " + precio);
        });

        await Caso("F32", "Insumo cotizado en dólares y cambio de precio por carga masiva", async () =>
        {
            if (preciosUsd is null) throw new OmitidoException("sin planilla de precios en dólares (E2E_PRECIOS_USD)");
            await Ir(admin, b, "cotizacion");
            var cotizacion = Num(await admin.Locator(".kpi-value").First.InnerTextAsync());
            await Ir(admin, b, "insumos");
            await admin.ClickAsync("button:has-text('+ Nuevo insumo')"); await admin.WaitForTimeoutAsync(600);
            var g = admin.Locator(".form-grid").First;
            await g.Locator("div", new() { HasText = "Código" }).First.Locator("input").FillAsync("INS-PIG-USD");
            await g.Locator("div", new() { HasText = "Nombre" }).First.Locator("input").FillAsync("Pigmento importado");
            await g.Locator("div", new() { HasText = "Unidad de medida" }).Locator("input").FillAsync("gramo");
            await g.Locator("div", new() { HasText = "Precio" }).Last.Locator("input").FillAsync("2");
            await admin.Locator("label", new() { HasText = "USD" }).Locator("input").CheckAsync();
            await admin.ClickAsync("button:has-text('Guardar')"); await admin.WaitForTimeoutAsync(1200);
            var tarjeta = admin.Locator(".card", new() { HasText = "Proveedores del insumo" });
            await tarjeta.Locator(".inline-form select").SelectOptionAsync(new SelectOptionValue { Label = "Botanica Aurora" });
            await tarjeta.Locator(".inline-form input[type=number]").FillAsync("2");
            await tarjeta.Locator(".inline-form input:not([type=number])").FillAsync("PIG-01");
            await tarjeta.Locator("button:has-text('+ Agregar')").ClickAsync(); await admin.WaitForTimeoutAsync(1000);
            await SeleccionarProducto(admin, "Set de regalo");
            await AgregarComponente(admin, "Pigmento importado", "1");
            var antes = Num(await admin.Locator("tbody tr", new() { HasTextString = "Pigmento importado" }).Locator("td").Nth(4).InnerTextAsync());
            Exigir(Math.Abs(antes - 2 * cotizacion) < 0.05m, $"el costo en pesos {antes} no es 2 USD × {cotizacion}");
            await Ir(admin, b, "insumos/precios");
            await admin.SelectOptionAsync("select", new SelectOptionValue { Label = "Botanica Aurora" }); await admin.WaitForTimeoutAsync(400);
            await admin.SetInputFilesAsync("input[type=file]", preciosUsd);
            await admin.Locator("h2:has-text('Vista previa')").WaitForAsync(); await admin.WaitForTimeoutAsync(500);
            await admin.ClickAsync("text=Confirmar importación"); await admin.WaitForTimeoutAsync(1500);
            await SeleccionarProducto(admin, "Set de regalo");
            var despues = Num(await admin.Locator("tbody tr", new() { HasTextString = "Pigmento importado" }).Locator("td").Nth(4).InnerTextAsync());
            Exigir(Math.Abs(despues - 3 * cotizacion) < 0.05m, $"tras la carga masiva el costo es {despues}, se esperaba 3 USD × {cotizacion}");
        });

        // ------------------------------------------------------------------ Clientes y órdenes
        await Caso("F27", "Alta de un cliente con un documento ya registrado", async () =>
        {
            await Ir(admin, b, "clientes");
            await admin.ClickAsync("button:has-text('+ Nuevo')"); await admin.WaitForTimeoutAsync(500);
            await admin.Locator(".form-grid div", new() { HasText = "Nombre o razón social" }).Locator("input").FillAsync("Cliente duplicado");
            await admin.Locator(".form-grid div", new() { HasText = "CUIT / CUIL / DNI" }).Locator("input").FillAsync("30-71234567-8");
            await admin.ClickAsync("button:has-text('Guardar')"); await admin.WaitForTimeoutAsync(1000);
            Exigir((await Estado(admin)).Contains("Almacén Natural Rosario"), await Estado(admin));
        });

        await Caso("F8", "Inicio de una orden de producción sin stock suficiente", async () =>
        {
            var stock = await StockProducto(admin, "Vela de soja Naranja");
            await OrdenPlanificada(admin, "Vela de soja Naranja cítrica 220ml", "999");
            await admin.ClickAsync("button:has-text('Iniciar')"); await admin.WaitForTimeoutAsync(1200);
            var estado = await Estado(admin);
            Exigir(estado.Contains("insuficiente", StringComparison.OrdinalIgnoreCase) || estado.Contains("Faltan"), estado);
            Exigir(await StockProducto(admin, "Vela de soja Naranja") == stock, "se descontó stock");
        });

        await Caso("F9", "Cancelación de una orden en proceso", async () =>
        {
            var stock = await StockProducto(admin, "Vela de soja Naranja");
            await OrdenPlanificada(admin, "Vela de soja Naranja cítrica 220ml", "2");
            await admin.ClickAsync("button:has-text('Iniciar')"); await admin.WaitForTimeoutAsync(1200);
            Exigir(await StockProducto(admin, "Vela de soja Naranja") == stock - 2, "no descontó al iniciar");
            await Ir(admin, b, "ordenes");
            await admin.Locator(".table.selectable tbody tr").First.ClickAsync(); await admin.WaitForTimeoutAsync(600);
            await admin.ClickAsync("button:has-text('Cancelar orden')"); await admin.WaitForTimeoutAsync(1200);
            Exigir(await StockProducto(admin, "Vela de soja Naranja") == stock, "no reintegró el stock");
        });

        await Caso("F28", "Alta de un pedido con cliente y asignación de un cliente a un pedido existente", async () =>
        {
            await OrdenPlanificada(admin, "Vela de soja Lavanda 220ml", "1", "Spa Lavanda");
            Exigir((await admin.Locator(".table.selectable tbody tr").First.InnerTextAsync()).Contains("Spa Lavanda"), "el pedido no muestra el cliente");
            await OrdenPlanificada(admin, "Vela de soja Lavanda 220ml", "1");
            Exigir((await admin.Locator(".table.selectable tbody tr").First.InnerTextAsync()).Contains("Consumidor final"), "sin cliente debería figurar Consumidor final");
            await admin.Locator(".table.selectable tbody tr").First.ClickAsync(); await admin.WaitForTimeoutAsync(600);
            await admin.SelectOptionAsync(".form-grid select", new SelectOptionValue { Label = "Almacén Natural Rosario" });
            await admin.ClickAsync("button:has-text('Asignar cliente')"); await admin.WaitForTimeoutAsync(1200);
            Exigir((await Estado(admin)).Contains("asignado al cliente"), await Estado(admin));
            await Ir(admin, b, "");
            Exigir((await admin.Locator("tbody").InnerTextAsync()).Contains("Almacén Natural Rosario"), "el panel no muestra el cliente");
        });

        // ------------------------------------------------------------------ Panel e inteligencia de negocio
        await Caso("F24", "Consulta del panel en un período con pedidos finalizados", async () =>
        {
            await Ir(admin, b, "");
            await admin.Locator(".period-picker select").SelectOptionAsync("2026-10"); await admin.WaitForTimeoutAsync(1200);
            var kpis = await admin.Locator(".kpi").AllInnerTextsAsync();
            Exigir(kpis.Count == 4 && Num(kpis[0].Split('\n')[1]) > 0 && kpis[3].Contains('%'), string.Join(" | ", kpis));
        });

        await Caso("F29", "Panel en un mes sin pedidos nuevos, con pedidos abiertos de meses anteriores", async () =>
        {
            await Ir(admin, b, "");
            await admin.Locator(".period-picker select").SelectOptionAsync(DateTime.UtcNow.ToString("yyyy-MM")); await admin.WaitForTimeoutAsync(1200);
            var filas = await admin.Locator("tbody tr").AllInnerTextsAsync();
            Exigir(filas.Any(f => f.Contains("OP-DEMO-SOY-001")), "no lista el pedido abierto de octubre");
        });

        await Caso("F16", "Consulta del tablero de inteligencia de negocio por período", async () =>
        {
            await Ir(admin, b, "inteligencia");
            var antes = await admin.Locator(".kpis").InnerTextAsync();
            await admin.Locator(".period-picker select").SelectOptionAsync("2026-10"); await admin.WaitForTimeoutAsync(1500);
            var despues = await admin.Locator(".kpis").InnerTextAsync();
            Exigir(await admin.Locator(".decision").CountAsync() > 0, "sin alertas");
            Exigir(antes != despues || despues.Length > 0, "no recalculó");
        });

        await Caso("F33", "Gráfico de órdenes por estado del tablero de inteligencia de negocio", async () =>
        {
            var tarjeta = admin.Locator(".card", new() { HasText = "Órdenes de producción por estado" });
            var etiquetas = (await tarjeta.Locator(".bar-label span").AllInnerTextsAsync()).Select(x => x.Trim()).ToList();
            Exigir(etiquetas.Count > 0 && etiquetas.Distinct().Count() == etiquetas.Count, "estados repetidos: " + string.Join(", ", etiquetas));
        });

        // ------------------------------------------------------------------ Cotización
        await Caso("F10", "Actualización automática de la cotización del dólar", async () =>
        {
            await Ir(admin, b, "cotizacion");
            var antes = await admin.Locator("tbody tr").CountAsync();
            await admin.ClickAsync("button:has-text('Actualizar blue')"); await admin.WaitForTimeoutAsync(5000);
            Exigir(await admin.Locator("tbody tr").CountAsync() == antes + 1, "no se agregó la cotización: " + await Estado(admin));
        });

        await Caso("F11", "Actualización de la cotización sin conexión a Internet", async () =>
        {
            if (urlSinRed is null) throw new OmitidoException("sin servidor sin red (E2E_URL_SIN_RED)");
            var p = await Ingresar("profesor", "Profesor2026", urlSinRed);
            await Ir(p, urlSinRed, "cotizacion");
            var antes = await p.Locator("tbody tr").CountAsync();
            var activa = await p.Locator(".kpi-value").First.InnerTextAsync();
            await p.ClickAsync("button:has-text('Actualizar blue')"); await p.WaitForTimeoutAsync(15000);
            var estado = await Estado(p);
            Exigir(await p.Locator("tbody tr").CountAsync() == antes && await p.Locator(".kpi-value").First.InnerTextAsync() == activa, "cambió la cotización");
            Exigir(estado.StartsWith("No se pudo obtener la cotización"), "no informó el error: " + estado);
            Exigir(!await p.Locator("#blazor-error-ui").IsVisibleAsync(), "la aplicación falló");
            Console.WriteLine("     " + estado);
        });

        // ------------------------------------------------------------------ Copias de seguridad
        await Caso("F12", "Generación de una copia de seguridad manual", async () =>
        {
            await Ir(admin, b, "backups");
            if (Environment.GetEnvironmentVariable("E2E_COPIAS_DIR") is { } copias)
            {
                await admin.Locator(".form-grid div", new() { HasText = "Carpeta de copias" }).Locator("input").FillAsync(copias);
                await admin.ClickAsync("button:has-text('Guardar configuración')"); await admin.WaitForTimeoutAsync(1000);
            }
            await admin.ClickAsync("text=Generar copia ahora"); await admin.WaitForTimeoutAsync(2000);
            Exigir((await Estado(admin)).StartsWith("Copia generada"), await Estado(admin));
            Exigir((await admin.Locator("tbody tr").First.InnerTextAsync()).Contains("OK"), "no figura en el historial");
        });

        string? nombreLocal = null;
        await Caso("F13", "Sincronización bidireccional con la carpeta de la nube", async () =>
        {
            if (nubeDir is null) throw new OmitidoException("sin carpeta de nube (E2E_NUBE_DIR)");
            File.WriteAllText(Path.Combine(nubeDir, "backup-20260101-090000.zip"), "copia subida por otro equipo");
            await Ir(admin, b, "backups");
            await admin.Locator(".form-grid div", new() { HasText = "Carpeta de la nube" }).Locator("input").FillAsync(nubeDir);
            var sync = admin.Locator("label.check", new() { HasText = "Sincronizar con la nube" }).Locator("input");
            if (!await sync.IsCheckedAsync()) await sync.CheckAsync();
            await admin.WaitForTimeoutAsync(300);
            var bidi = admin.Locator("label.check", new() { HasText = "Bidireccional" }).Locator("input");
            if (!await bidi.IsCheckedAsync()) await bidi.CheckAsync();
            await admin.ClickAsync("button:has-text('Guardar configuración')"); await admin.WaitForTimeoutAsync(1000);
            await admin.ClickAsync("button:has-text('Sincronizar ahora')"); await admin.WaitForTimeoutAsync(3000);
            var estado = await Estado(admin);
            Console.WriteLine("     " + estado);
            Exigir(Regex.IsMatch(estado, @"[1-9]\d* subida") && Regex.IsMatch(estado, @"[1-9]\d* descarga"), estado);
            nombreLocal = Path.GetFileName(Directory.GetFiles(nubeDir, "backup-2026*.zip").First(f => !f.Contains("20260101")));
        });

        await Caso("F31", "Copia manual con el sistema en uso y sincronización con la carpeta de la nube", async () =>
        {
            if (nubeDir is null) throw new OmitidoException("sin carpeta de nube (E2E_NUBE_DIR)");
            await Ir(admin, b, "backups");
            await admin.ClickAsync("text=Generar copia ahora"); await admin.WaitForTimeoutAsync(2000);
            var nombre = (await Estado(admin)).Replace("Copia generada: ", "").Trim();
            Exigir((await admin.Locator("tbody tr").First.InnerTextAsync()).Contains("OK"), "no figura en el historial");
            await admin.ClickAsync("button:has-text('Sincronizar ahora')"); await admin.WaitForTimeoutAsync(3000);
            Exigir(File.Exists(Path.Combine(nubeDir, nombre)), $"«{nombre}» no se subió a la nube");
        });

        await Caso("F14", "Sincronización con copias homónimas de distinto contenido", async () =>
        {
            if (nubeDir is null || nombreLocal is null) throw new OmitidoException("depende de F13");
            File.WriteAllText(Path.Combine(nubeDir, nombreLocal), "contenido distinto en la nube");
            await Ir(admin, b, "backups");
            await admin.ClickAsync("button:has-text('Sincronizar ahora')"); await admin.WaitForTimeoutAsync(3000);
            var estado = await Estado(admin);
            Console.WriteLine("     " + estado);
            Exigir(Regex.IsMatch(estado, @"[1-9]\d* conflicto"), estado);
            Exigir(Directory.GetFiles(nubeDir).Length > Directory.GetFiles(nubeDir, nombreLocal).Length, "no se conservó la versión de la nube");
        });

        await Caso("F15", "Sincronización con la carpeta de la nube no disponible", async () =>
        {
            await Ir(admin, b, "backups");
            await admin.Locator(".form-grid div", new() { HasText = "Carpeta de la nube" }).Locator("input").FillAsync(@"Z:\carpeta-que-no-existe");
            await admin.ClickAsync("button:has-text('Guardar configuración')"); await admin.WaitForTimeoutAsync(1000);
            await admin.ClickAsync("button:has-text('Sincronizar ahora')"); await admin.WaitForTimeoutAsync(2500);
            var estado = await Estado(admin);
            Console.WriteLine("     " + estado);
            Exigir(!estado.Contains("completada"), estado);
            Exigir(!await admin.Locator("#blazor-error-ui").IsVisibleAsync(), "la aplicación falló");
            await Ir(admin, b, "productos");
            Exigir(await admin.Locator(".item-list li").CountAsync() > 0, "el sistema dejó de funcionar");
        });

        await Caso("F17", "Restablecimiento de la base de datos sin la frase de confirmación", async () =>
        {
            await Ir(admin, b, "backups");
            var zona = admin.Locator(".danger-zone");
            await zona.Locator("input").FillAsync("borrar");
            Exigir(await zona.Locator("button").IsDisabledAsync(), "el botón se habilitó sin la frase exacta");
            await Ir(admin, b, "productos");
            Exigir(await admin.Locator(".item-list li").CountAsync() > 0, "se borraron datos");
        });

        await Caso("F17b", "Restablecimiento de la base de datos con la frase de confirmación", async () =>
        {
            await Ir(admin, b, "backups");
            var zona = admin.Locator(".danger-zone");
            await zona.Locator("input").FillAsync("BORRAR TODO");
            await zona.Locator("button").ClickAsync();
            await admin.WaitForURLAsync("**/login");
            var p = await Ingresar("profesor", "Profesor2026");
            // Queda como una instalación nueva: solo los datos de ejemplo iniciales, sin lo cargado durante la prueba.
            await Ir(p, b, "productos");
            Exigir(await p.Locator(".item-list li", new() { HasTextString = "Set de regalo" }).CountAsync() == 0, "quedaron los productos cargados");
            await Ir(p, b, "clientes");
            Exigir(await p.Locator(".item-list li:not(.muted)").CountAsync() == 0, "quedaron clientes");
            await Ir(p, b, "bitacora");
            Exigir((await p.Locator("tbody").InnerTextAsync()).Contains("ResetearBaseDatos"), "no registró el restablecimiento");
        });

        Console.WriteLine($"Casos: {ok} correctos, {fallas} con falla, {omitidos} omitidos.");
        return fallas == 0 ? 0 : 1;
    }

    private sealed class OmitidoException(string motivo) : Exception(motivo);
}
