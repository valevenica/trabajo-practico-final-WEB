# Ejemplos de pruebas funcionales (versión web)

Datos de ejemplo, pasos y resultado esperado de los casos F1 a F53 de la sección Prueba del documento del proyecto.
Todos se ejecutan sobre una copia de la base de demostración (`demo/StockManufactura-demo.db`), con el servidor
iniciado con `Demo__Habilitada=true`. Los casos F1 a F33 están automatizados en `tests/StockManufactura.Web.E2E`
(`dotnet run -- casos`); los F34 a F46, en la suite de extremo a extremo y en `tests/StockManufactura.Web.Tests`.

## Datos de la base de demostración

| Elemento | Valores |
|---|---|
| Usuarios | `profesor` / `Profesor2026` (Administrador), `produccion` / `Produccion2026` (Producción), `consulta` / `Consulta2026` (Consulta) |
| Productos | Vela de soja Lavanda 220ml, Vela de soja Naranja cítrica 220ml |
| Insumos | Cera de soja natural (kg), Aceite esencial de lavanda (ml), Aceite esencial de naranja (ml), Frasco ámbar 220ml con tapa, Mecha de algodón encerada; servicio: Electricidad del taller (kWh, por tramos) |
| Proveedores | Botanica Aurora (CUIT 30-77999111-3), Ceras del Litoral (CUIT 30-70555444-1) |
| Clientes | Spa Lavanda, Almacén Natural Rosario |
| Pedidos | OP-DEMO-SOY-001 (planificado), OP-DEMO-SOY-002 y OP-DEMO-SOY-003 (finalizados en octubre de 2026) |

Recursos auxiliares para los casos que dependen de servicios externos:

| Recurso | Casos | Cómo se prepara |
|---|---|---|
| Servidor de correo de prueba | F19, F20 | Un servidor SMTP en `localhost:2525` que guarda los mensajes en una carpeta; se configura en Usuarios > Correo de recuperación. |
| Carpeta de nube | F13, F14, F15, F31 | Una carpeta local vacía que hace de carpeta sincronizada. |
| Servidor sin Internet | F11 | Un segundo servidor sobre la misma base, iniciado con `HTTPS_PROXY=http://127.0.0.1:9`. |
| Planilla en dólares | F32 | Excel con la fila `PIG-01` / `3`. |
| Ejemplos del proveedor | F49 a F51 | Carpeta `Ejemplos_StockManufactura`: `1_Precios_Ceras_del_Litoral_alta.xlsx`, `2_Factura_ficticia_Ceras_del_Litoral.pdf`, `3_Precios_Ceras_del_Litoral_aumento.xlsx` y `LEEME.txt`. |

## Acceso y seguridad

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F1 | Ingresar con `profesor` / `Profesor2026`. Abrir Bitácora. | Se abre el Panel principal y la bitácora registra `LoginExitoso`. |
| F2 | Ingresar cinco veces con `consulta` y una contraseña incorrecta. | La cuenta queda bloqueada temporalmente y el mensaje lo informa. |
| F18 | Ingresar con `consulta` y escribir la dirección `/usuarios`. | El menú no muestra Usuarios y la página informa acceso denegado. |
| F19 | Configurar el correo de prueba. En la pantalla de ingreso, «¿Olvidó su contraseña?» con `produccion@demo.local`; usar el código de seis dígitos recibido y la contraseña nueva `Recuperada2026`. | Llega el código con el nombre de usuario; se define la contraseña nueva y se ingresa sin reiniciar la aplicación. |
| F20 | Repetir la recuperación con el código `000000` y la contraseña `OtraClave2026`. | Se rechaza el código y la contraseña no cambia. |
| F21 | En Usuarios, seleccionar la cuenta bloqueada de F2 y asignar la contraseña temporal `Temporal2026`. | Se informa «Contraseña temporal asignada» y la cuenta se desbloquea. |
| F22 | Ingresar con esa cuenta y `Temporal2026`. | Se abre «Debe cambiar su contraseña»; los demás módulos quedan bloqueados hasta cambiarla. |
| F25 | Ingresar con `profesor@demo.local` / `Profesor2026`. | Se rechaza con «Credenciales inválidas»: se ingresa con el nombre de usuario. |
| F26 | Crear un usuario con el nombre `PROFESOR`. | Se rechaza porque el nombre ya existe. |
| F38 | Ingresar con `produccion`. | El menú no muestra costos ni administración y `/usuarios` se rechaza. |
| F39 | Pedir la recuperación para `noexiste@demo.local`. | El mensaje es el mismo que para un correo registrado. |
| F41, F42 | Abrir el sistema en un teléfono (390 × 844) y recorrer las 18 pantallas internas. | El menú se pliega bajo «☰ Menú» y ninguna pantalla se desplaza en horizontal. |
| F44 | Ingresar desde `/login?ReturnUrl=https://sitio-externo.com`. | El usuario ingresa y permanece en el sistema. |
| F45 | Abrir la dirección de descarga de una copia sin sesión y luego con `produccion`. | Sin sesión se pide el ingreso; sin permiso, HTTP 403. |
| F46 | Cerrar sesión y volver atrás en el navegador. | Toda pantalla vuelve a pedir el ingreso. |
| F53 | En una computadora, presionar « junto al nombre del sistema y recargar la página. | El menú sigue oculto; el botón ☰ lo vuelve a mostrar. |

## Productos, insumos y costeo

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F3 | Crear un producto con el código `PRD-VELA-LAV-220`. | «Ya existe un producto con ese código.» |
| F4 | En «Vela de soja Lavanda 220ml», agregar un insumo a la receta. | El costo y el precio sugerido suben y se agrega un registro al historial de costos. |
| F5 | Crear «Set de regalo» con `[Producto] Vela de soja Lavanda` y `[Producto] Vela de soja Naranja` como componentes. | El costo del set es la suma de los costos de las dos velas. |
| F6 | Agregar «Electricidad del taller» a la receta del set. | El costo de la electricidad se resuelve por el tramo que corresponde al consumo. |
| F23 | Cambiar el stock de «Vela de soja Naranja» desde su ficha. | El listado muestra el valor nuevo. |
| F34 | Desactivar «Vela de soja Lavanda», que integra el set. | Sale del listado y de la selección de componentes, sigue en el set y se reactiva desde «Mostrar inactivos». |
| F35 | Desactivar un insumo y abrir la receta de un producto. | No se ofrece en las recetas nuevas; al reactivarlo vuelve a estar disponible. |
| F43 | Abrir y guardar un producto sin cambios. | El margen se muestra en su campo y no cambia al guardar. |
| F30 | En «Cera de soja natural», agregar «Ceras del Litoral» con el código `CL-CERA-01` y el precio `13.40`, marcarlo prioritario y quitar el otro proveedor. | El precio del insumo pasa a 13,40. |
| F32 | Crear el insumo `INS-PIG-USD` «Pigmento importado» en dólares, vincularlo a «Botanica Aurora» con el código `PIG-01`, agregarlo al set e importar la planilla en dólares. | El costo se expresa en pesos con la cotización prioritaria y se actualiza con el precio nuevo. |

## Listas de precios y facturas de proveedores

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F7 | Carga masiva de precios con el proveedor «Botanica Aurora» y la planilla `precios.xlsx` de la suite. | Las filas se asocian por el código del proveedor («Listo» o «Sin cambios») y se importan. |
| F49 | Carga masiva con «Ceras del Litoral» y `1_Precios_Ceras_del_Litoral_alta.xlsx`. Con «Agregar», vincular `CL-CERA-01`, `CL-MECHA-15` y `CL-FRASCO-220` a la cera, la mecha y el frasco. | Los cuatro códigos figuran como «No encontrado»; los vinculados pasan a «Sin cambios». |
| F50 | Con «Agregar» en `CL-PARAF-05`, elegir «Crear insumo nuevo»: código `PAR-ESC`, nombre «Parafina en escamas», unidad `kg`. | Se crea el insumo con el precio 9,50 vinculado al proveedor. |
| F51 | En Ingresos, «Cargar desde factura (PDF)» con `2_Factura_ficticia_Ceras_del_Litoral.pdf`; agregar a la lista y registrar el ingreso. | Se reconoce el proveedor por el CUIT y los cuatro insumos por su código; «Flete y embalaje» queda sin ingresar. El stock sube: +25 kg de cera, +200 mechas, +48 frascos y +10 kg de parafina. |
| — | Carga masiva con `3_Precios_Ceras_del_Litoral_aumento.xlsx`. | Tres precios «Listo», uno «Sin cambios» y `CL-XX-999` con precio inválido. |

## Órdenes y stock

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F8 | Crear y planificar una orden de «Vela de soja Naranja cítrica 220ml» por más unidades que su stock; presionar Iniciar. | Se informa «Faltan…» y no se descuenta stock. |
| F9 | Crear una orden de 2 unidades de la misma vela con stock suficiente, iniciarla y cancelarla. | Al iniciar se descuentan 2 unidades; al cancelar se reintegran. |
| F28 | Crear un pedido de «Vela de soja Lavanda 220ml» con el cliente «Spa Lavanda» y otro sin cliente; asignar a este «Almacén Natural Rosario». | El primero muestra el cliente, el segundo «Consumidor final» hasta asignarle el cliente; el panel lo refleja. |
| F36 | En el Panel principal, avanzar con ▶ un pedido sin stock suficiente. | Se informa el faltante y el pedido conserva su estado. |
| F47 | Crear una orden con 2 velas de lavanda, 3 de naranja y otra vela de lavanda; luego quitar una línea y presionar «Guardar productos». | La orden tiene una línea por producto (lavanda × 3) y sus productos cambian mientras está en espera. |
| F48 | Iniciar una orden con las dos velas cuando a una le falta stock. | Se detalla el faltante de cada producto y no se descuenta ninguno. |

## Cotización, indicadores y bitácora

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F10 | En Configuración monetaria, «🔄 Actualizar blue» con conexión. | Se agrega una cotización automática al historial. |
| F11 | Lo mismo en el servidor sin Internet. | «No se pudo obtener la cotización…»; la cotización activa no cambia. |
| F52 | Cotizar «Frasco ámbar 220ml con tapa» en USD (0,15) y guardar una cotización manual de 1.450. | El mensaje informa que se recalcularon automáticamente los insumos en dólares y los productos. |
| F16 | En Inteligencia de negocio, cambiar el período. | Se recalculan los indicadores, los gráficos y las alertas. |
| F24 | En el Panel principal, elegir octubre de 2026. | Ventas, pedidos, ticket promedio y ganancia de los pedidos finalizados del mes. |
| F29 | Elegir el mes actual, sin pedidos nuevos. | `OP-DEMO-SOY-001`, abierto desde octubre, figura en el listado. |
| F33 | Observar el gráfico de órdenes por estado. | Cada estado aparece una sola vez. |
| F37 | En Bitácora, filtrar por el módulo `Autenticacion` y exportar CSV. | Solo eventos de ese módulo y un archivo CSV con todos los resultados. |

## Clientes

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F27 | Crear un cliente con el documento de «Almacén Natural Rosario». | Se rechaza indicando el cliente que ya tiene ese documento. |

## Copias de seguridad y restablecimiento

| Caso | Datos y pasos | Resultado esperado |
|---|---|---|
| F12 | En Backups, «Generar copia ahora». | «Copia generada» y un registro OK en el historial. |
| F13 | Dejar en la carpeta de nube `backup-20260101-090000.zip` («copia subida por otro equipo»), activar la sincronización bidireccional y sincronizar. | Se suben las copias locales que faltan y se descarga la del otro equipo. |
| F14 | Cambiar el contenido de una copia homónima en la nube y sincronizar. | Se conservan las dos versiones y se informa el conflicto. |
| F15 | Indicar una carpeta de nube inexistente y sincronizar. | Se omite la sincronización informando el motivo; la aplicación sigue funcionando. |
| F31 | Generar una copia con el sistema en uso y sincronizar. | La copia figura en el historial y en la carpeta de nube. |
| F40 | Restaurar una copia sin escribir `RESTAURAR`. | La operación se cancela y los datos no cambian. |
| F17 | Restablecer la base sin escribir `BORRAR TODO`. | El botón queda deshabilitado y no se borra nada. |
| F17 | Restablecer con `BORRAR TODO` y confirmar. | Se vuelve al ingreso; no quedan «Set de regalo» ni clientes, y la bitácora registra `ResetearBaseDatos`. |
