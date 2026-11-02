# Pruebas de punta a punta de la versión web

Recorren el sistema en Chrome como lo haría un usuario: ingreso, los 16 módulos del menú, desactivar y reactivar
productos e insumos, insumos especiales, órdenes, cotización, carga masiva de precios, bitácora, copias de seguridad,
usuarios y roles, permisos por rol y la vista de celular. Son 40 controles.

**Modifican datos** (crean usuarios y roles, restauran una copia, desactivan y reactivan registros). Usarlas solo con
una base de prueba, nunca con la base real.

## Requisitos

- Google Chrome instalado (se usa el canal `chrome` de Playwright, no hace falta descargar navegadores).
- Una base de prueba con al menos dos productos, un insumo y un proveedor, y el usuario administrador con la contraseña
  ya cambiada (no pendiente de cambio).

## Ejecución

1. Levantar la web sobre la base de prueba:

   ```powershell
   cd src/StockManufactura.Web
   $env:ConnectionStrings__StockManufactura = "Data Source=C:\ruta\a\prueba.db"
   dotnet run --urls http://localhost:5199
   ```

2. En otra consola:

   ```powershell
   cd tests/StockManufactura.Web.E2E
   $env:E2E_PASSWORD = "contraseña del administrador de la base de prueba"
   dotnet run
   ```

Variables opcionales: `E2E_URL` (por defecto `http://localhost:5199/`) y `E2E_USUARIO` (por defecto `Admin`).

El programa termina con código 0 si todos los controles pasan. Las capturas de las fallas y del menú de celular quedan
en `bin/Debug/net10.0/capturas`.

## Casos funcionales F1 a F33

`dotnet run -- casos` ejecuta los casos de prueba funcionales del documento del proyecto sobre la base de demostración
(`demo/StockManufactura-demo.db`), con el servidor iniciado con `Demo__Habilitada=true`. Al final restablece la base, así
que conviene usar una copia. Algunos casos necesitan recursos adicionales, indicados con variables de entorno; si faltan,
el caso se informa como omitido:

| Variable | Casos | Contenido |
|---|---|---|
| `E2E_CORREO_DIR` | F19, F20 | Carpeta donde un servidor SMTP de prueba en `localhost:2525` guarda los mensajes. |
| `E2E_NUBE_DIR` | F13, F14, F31 | Carpeta vacía que hace de carpeta sincronizada de la nube. |
| `E2E_COPIAS_DIR` | F12 | Carpeta para las copias de seguridad de la prueba. |
| `E2E_URL_SIN_RED` | F11 | Otro servidor iniciado con `HTTPS_PROXY=http://127.0.0.1:9` (sin acceso a Internet). |
| `E2E_PRECIOS_USD` | F32 | Planilla con la fila `PIG-01` / `3` (código y precio en dólares). |
