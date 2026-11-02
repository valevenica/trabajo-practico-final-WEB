# Integra Manufacturing — StockManufactura

Sistema web de gestión de inventario, producción y costos de fabricación para pequeñas y medianas empresas
manufactureras. Proyecto Final de Ingeniería en Sistemas de Información (UTN Facultad Regional Rosario).

Aplicación **ASP.NET Core Blazor Server** (`src/StockManufactura.Web`) con arquitectura limpia: Domain, Application,
Infrastructure y Shared. Persistencia en **SQLite** mediante Entity Framework Core.

## Ejecutar

```
dotnet run --project src/StockManufactura.Web
```

Luego abrir la URL que indica la consola. Al iniciar se aplican las migraciones y se crea el usuario administrador
(`Admin` / `Admin123`; se pide cambiarla en el primer ingreso).

### Base de datos

SQLite, en un único archivo: por defecto `Documentos\StockManufactura\Web\Data\StockManufactura.db` (la carpeta
`Documentos\StockManufactura\Web` guarda también las copias de seguridad y la configuración del correo). Para usar otro
archivo, completar `ConnectionStrings:StockManufactura` en `appsettings.json` o en la variable de entorno
`ConnectionStrings__StockManufactura`, por ejemplo `Data Source=C:\datos\stock.db`. Las copias de seguridad, la
restauración y el restablecimiento operan siempre sobre el archivo de esa conexión.

### Módulos

Menú agrupado (Dashboard, Stock, Proveedores, Clientes, Producción, Finanzas y Administración) con permisos por rol:

- Ingreso con opción de mostrar la contraseña, «¿Olvidó su contraseña?» (código por correo) y registro de cada intento
  en la bitácora. «Mi cuenta» para editar los datos propios y cambiar la contraseña.
- Panel principal (resumen de ventas filtrable por mes y pedidos con filtros por estado, orden y búsqueda),
  inteligencia de negocio y faltantes.
- Productos (con receta e historial de costos), insumos, insumos especiales (tramos de precio y simulador de consumo),
  ingresos de stock y carga masiva de precios desde Excel.
- Proveedores, clientes y órdenes de producción.
- Configuración monetaria (dólar blue automático, carga manual e historial).
- Usuarios, roles y permisos, y cuenta de correo para la recuperación de contraseñas.
- Copias de seguridad (manuales, automáticas, descarga, restauración y sincronización con la nube), restablecimiento
  de la base de datos y bitácora de auditoría con exportación a CSV.
- Ayuda en línea: el manual de usuario (`wwwroot/ayuda/manual-de-usuario.pdf`), enlazado desde el menú y el ingreso.

Productos e insumos no se eliminan: se desactivan y se pueden reactivar desde «Mostrar inactivos».

### Demo publicada (Render, plan gratuito)

El repositorio incluye todo lo necesario para publicar una demo con un link:

- `Dockerfile`: compila la web y copia `demo/StockManufactura-demo.db` (datos ficticios de un taller de velas) dentro de la imagen.
  Cada vez que el servicio se reinicia, los datos vuelven a ese estado inicial.
- `render.yaml`: en [Render](https://render.com), *New > Blueprint*, elegir este repositorio; crea el servicio
  `stockmanufactura-demo` en el plan gratuito.
- Modo demo (`Demo__Habilitada=true`): en cada arranque deja activos, desbloqueados y con su contraseña los usuarios
  `profesor` / `Profesor2026` (administrador), `produccion` / `Produccion2026` y `consulta` / `Consulta2026`, y los
  muestra en la pantalla de ingreso.

En el plan gratuito el servicio se suspende tras 15 minutos sin uso; la primera visita posterior tarda alrededor de un
minuto. La recuperación de contraseña por correo no está configurada en la demo.

### Pruebas automáticas

| Proyecto | Qué cubre | Cómo se ejecuta |
|---|---|---|
| `tests/StockManufactura.UnitTests` | Servicios de las capas de aplicación e infraestructura (75 pruebas). | `dotnet test` |
| `tests/StockManufactura.Web.Tests` | Lógica propia de la web (períodos, permisos por módulo, sesión, movimiento de stock de pedidos) y pruebas de integración HTTP sobre el sitio completo en memoria: ingreso, cambio de contraseña obligatorio, redirección segura, menú por rol, descarga de copias (401/403/404) cierre de sesión, copias sobre la base configurada, restablecimiento de la base y modo demostración (66 pruebas). | `dotnet test` |
| `tests/StockManufactura.Web.E2E` | 40 controles en Chrome contra un servidor levantado, incluida la vista de celular. Modifica datos: usar solo con una base de prueba. | Ver su `README.md` |

---

Notas de diseño de los módulos.

## Pruebas funcionales guiadas

- Ver ejemplos por modulo en `docs/ejemplos-pruebas-funcionales.md`.

## Diseno actual de recursos y cotizacion monetaria

El sistema ya no sincroniza precios de recursos desde proveedores externos.

- Los precios de recursos se cargan manualmente por el usuario.
- La unica actualizacion automatica es la cotizacion del dolar.
- Todos los costos de fabricacion se calculan en ARS.

## Proveedores

Proveedor representa una entidad comercial y no participa en sincronizacion automatica.

Campos principales:

- Nombre
- Razon social
- CUIT
- Direccion
- Ciudad
- Provincia
- Pais
- Telefono
- Email
- Persona de contacto
- Observaciones
- Activo
- Fecha de alta

## Recursos

Recurso incluye:

- Codigo
- Nombre
- Descripcion
- Categoria
- Unidad de medida
- Stock actual
- Stock minimo
- Precio
- Moneda (ARS, USD)
- Proveedor habitual
- Fecha ultima actualizacion
- Observaciones
- Activo

Reglas:

- Si moneda = ARS, costo = precio ingresado.
- Si moneda = USD, costo = precio USD x cotizacion vigente.

## Configuracion Monetaria

Modulo dedicado para administrar cotizacion del dolar:

- Ver cotizacion actual
- Actualizacion manual
- Actualizacion automatica
- Seleccion de fuente
- Historial de cotizaciones

Entidad principal: ExchangeRate

- Valor
- Fecha
- Fuente
- Usuario
- Automatica

### Open/Closed

Se utiliza la interfaz IExchangeRateProvider para soportar nuevas fuentes sin modificar el servicio central.

Implementacion inicial:

- DolarHoyProvider (placeholder en esta fase)

Futuras fuentes posibles:

- Banco Nacion
- BCRA
- Bluelytics
- APIs privadas

## Historial y trazabilidad

Cada cambio de precio de recurso genera ResourcePriceHistory con:

- Fecha
- Usuario
- Precio anterior
- Precio nuevo
- Moneda
- Cotizacion utilizada
- Observaciones

Cada calculo de costo genera ResourceCostCalculation con:

- Recurso
- Fecha de calculo
- Cotizacion utilizada
- Costo en pesos

## Backups

La informacion de cotizaciones e historiales forma parte del backup SQLite porque se almacena en las mismas tablas de la base principal.

## Arquitectura

Se mantiene:

- Clean Architecture
- Componentes Blazor sin lógica de negocio (la lógica vive en Application)
- Repository Pattern
- Unit Of Work
- SOLID

Toda la lógica de negocio se implementa en Application. Las páginas Blazor no contienen lógica de negocio.
