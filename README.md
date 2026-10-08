# Sistema de facturación · Ferretería

Aplicación de escritorio para facturar rápido en mostrador: C# (.NET 10) en el servidor local,
interfaz en HTML/CSS/JavaScript y base de datos SQLite en un solo archivo. Funciona **sin internet**.
Las facturas se imprimen sobre el **talonario preimpreso** con una impresora de matriz de punto.

```
┌──────────────── Facturacion.exe (ventana Windows) ────────────────┐
│  WebView2 (motor de Edge) ── muestra ──▶ interfaz HTML (wwwroot)  │
│        │ imprime en silencio                    │ fetch /api      │
│        ▼                                        ▼                 │
│  PuenteImpresion               API local en http://127.0.0.1      │
│                                Core (reglas) → Datos (SQLite)     │
└───────────────────────────────────────────────────────────────────┘
```

## Ejecutar

Requisitos para desarrollar: .NET SDK 10 y Windows con WebView2 Runtime (ya viene en Windows 10/11).

| Para… | Comando |
|---|---|
| Abrir la aplicación de escritorio | `dotnet run --project src/Facturacion.Escritorio` |
| Abrirla en el navegador (depurar con F12) | `dotnet run --project src/Facturacion.Servidor` → http://127.0.0.1:5080 |
| Pruebas automáticas | `dotnet test` |
| Generar el instalable para la laptop | `.\publicar.ps1` (carpeta `publicar\`) |

En modo desarrollo los archivos de `wwwroot` se sirven directo desde el disco: basta con recargar
(F5) para ver cambios de HTML/CSS/JS. En producción van incrustados dentro del `.dll`.

Para usar otra carpeta de datos (por ejemplo, de pruebas): `-- --Datos:Carpeta=C:\ruta`.

## Estructura (modular)

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `Facturacion.Core` | Modelos, reglas de negocio y contratos (interfaces). Sin dependencias externas. | — |
| `Facturacion.Datos` | SQLite con Dapper, migraciones, Excel/CSV (ClosedXML), respaldos. | Core |
| `Facturacion.Web` | API HTTP, plantillas de impresión (HTML generado en C#) e interfaz (`wwwroot`). | Core, Datos |
| `Facturacion.Escritorio` | Ventana WinForms + WebView2, impresión directa, PDF, "Guardar como". | Web |
| `Facturacion.Servidor` | Host de consola para usar la aplicación en el navegador. | Web |
| `tests/Facturacion.Tests` | Pruebas de reglas y de base de datos (SQLite temporal). | Core, Datos |

Cada módulo funcional atraviesa las capas con el mismo nombre:

| Módulo | Core | Datos | API (`Web/Modulos`) | Pantalla (`wwwroot/js/modulos`) |
|---|---|---|---|---|
| Facturación | `Facturas/` | `FacturaRepositorio` | `FacturasEndpoints` | `facturacion.js`, `historial.js` |
| Catálogo | `Catalogo/` | `ProductoRepositorio`, `CategoriaRepositorio`, `UnidadRepositorio` | `CatalogoEndpoints` | `productos.js` |
| Inventario | `Inventario/`, `Importacion/` | `InventarioRepositorio`, `Archivos/` | `InventarioEndpoints` | `productos.js` |
| Clientes | `Clientes/` | `ClienteRepositorio` | `ClientesEndpoints` | `clientes.js` |
| Cierre de caja | `Reportes/` | `ReporteRepositorio` | `ReportesEndpoints` | `cierre.js` |
| Configuración | `Configuracion/` | `ConfiguracionRepositorio`, `Respaldos/` | `ConfiguracionEndpoints` | `configuracion.js` |
| Impresión | — | — | `ImpresionEndpoints` + `Web/Impresion/` | `impresion.js` |
| Validación | `Comun/Identificacion.cs` | — | (en cada servicio) | `validacion.js` |

La conexión entre interfaces e implementaciones está en un solo lugar: `Web/Composicion.cs`.

**Agregar un módulo nuevo:** modelos y servicio en `Core/<Modulo>`, repositorio en `Datos/Repositorios`,
registro en `Composicion.cs`, `Web/Modulos/<Modulo>Endpoints.cs` (y llamarlo en `AplicacionWeb.cs`),
`wwwroot/js/modulos/<modulo>.js` (exporta `montar`) y su ruta en `wwwroot/js/app.js`.
**Cambiar la base de datos:** agregar un paso nuevo al final de `Datos/Migraciones.cs` (nunca editar los anteriores).
La versión 2 agregó la modificación de facturas, las unidades y las posiciones medidas del talonario.

## Funciones

- **Facturación acelerada:** buscador con autocompletado desde 2 caracteres (máximo 20 resultados), por código,
  nombre, categoría o código de barras, sin importar tildes ni mayúsculas. Lector de código de barras (código + Enter).
  `5*código` agrega 5 unidades; un producto repetido suma cantidad. Precio editable por línea y artículos libres (F4).
  Máximo 22 líneas por factura (las del talonario).
- **Cliente:** Consumidor Final por defecto; autocompletado de clientes guardados y opción "Guardar cliente".
- **Totales:** subtotal, IVA configurable (desactivado por defecto: el talonario es de **Cuota Fija**) y vuelto con
  botones de billetes. El servidor recalcula todo antes de guardar.
- **Numeración:** serie y siguiente número sincronizados con el talonario físico, rango autorizado (avisa cuando
  quedan 50 y bloquea al agotarse) y nunca se repite un número.
- **Modificar una factura emitida:** en Historial, *Modificar* la abre en la pantalla de facturación con sus datos.
  Al guardar se comparan los cambios; si los hay, se muestran y son **obligatorios el motivo y el nombre del empleado**.
  La factura conserva su número y fecha, las existencias se ajustan por la diferencia y el historial la marca
  **MODIFICADA**, con la constancia de cada cambio (quién, cuándo, por qué y qué cambió; se guarda también la versión anterior).
- **Anular:** exige motivo, devuelve existencias y la factura queda registrada como ANULADA.
- **Catálogo:** alta/edición, cambio rápido de precio (clic en el precio), categorías y **unidades de medida**
  administrables (METRO, YARDA…; renombrar una unidad la cambia en todos sus productos), stock mínimo con alertas,
  ajustes de existencias (entrada, salida, conteo) con historial de movimientos.
- **Modificar en bloque:** se marcan productos (buscando entre varias búsquedas) y se les asigna una **categoría** o una
  **unidad**; antes de aplicar se confirma "Estás a punto de modificar N productos a categoría X". Por diseño solo
  existen esos dos campos: nombre, precio, código, código de barras y existencias no se cambian en bloque.
- **Validación de datos (Nicaragua):** teléfono de 8 dígitos (89704920); cédula `000-000000-0000A` con fecha de
  nacimiento y letra de control válidas; RUC de 14 caracteres según el aviso de la DGI (13 dígitos + letra, o
  J/N/R/E + 13 dígitos; no se aceptan los que inician con cuatro ceros). Se revisa al salir del campo y en el servidor.
- **Importar/Exportar:** Excel (.xlsx) o CSV (`,` o `;`, UTF-8 o ANSI). UPSERT por código en una sola transacción:
  no duplica, actualiza precios y existencias; las celdas vacías conservan el valor actual; crea categorías y
  unidades nuevas; informa errores por fila. La exportación sirve para conteo físico y se puede volver a importar.
- **Cierre de caja:** total por forma de pago, anuladas, numeración usada, productos más vendidos; imprimible en hoja carta.
- **Respaldos:** copia automática diaria (30 días), descarga manual y restauración (guarda antes una copia).

### Atajos de teclado

| Tecla | Acción | Tecla | Acción |
|---|---|---|---|
| F1 | Buscar producto | F8 | Efectivo recibido |
| F2 | Cliente | F10 (o F9, Ctrl+Enter) | Generar factura / guardar cambios |
| F4 | Artículo libre | Esc | Cerrar lista / limpiar o cancelar modificación |
| Enter | Agregar / confirmar | Alt+1 … Alt+6 | Cambiar de módulo |

La factura en curso (y una modificación a medias) se guarda sola: si se cierra la ventana, al volver sigue ahí.

## Impresión en el talonario

Flujo: **Generar la factura** (F10) → la pantalla indica **qué formulario colocar** ("Coloque el formulario
No. 40658") → se coloca el talonario alineado en el tractor de arrastre → **Imprimir** (Enter). Solo se imprimen
los datos (fecha, cliente, dirección, líneas y total) sobre el diseño preimpreso. *Vista previa* muestra la factura
completa en hoja carta para ver cómo quedará (también sirve para guardar el PDF).

En la versión de escritorio se imprime **directo, sin cuadro de diálogo**, en la impresora elegida en
Configuración > Impresión. En el controlador de la impresora configure el papel **Carta (216 × 279 mm)**.

**Posiciones:** se midieron sobre la foto del talonario corrigiendo la perspectiva (22 filas de 6.16 mm desde
y = 74.3 mm; columnas CANT. 8–27 mm, DESCRIPCIÓN 27–151 mm, P. UNIT. 151–178 mm, TOTAL 178–206 mm). Para afinarlas:

1. Configuración > Impresión > *Ver hoja de prueba* (cuadrícula en milímetros y las 22 filas llenas).
2. *Imprimir hoja de prueba* sobre un formulario (o en papel en blanco y sobrepóngalo a contraluz).
3. Si todo sale corrido, ajuste *Desplazamiento X/Y*; si un campo puntual no calza, edite *Posiciones avanzadas*
   (cada campo es una caja x, y, ancho, alto en mm; el texto se centra verticalmente en la caja).

**Impresora:** el cliente mantiene el talonario autorizado (original + copias), así que se necesita una impresora de
**matriz de punto**, la única que imprime a través del papel autocopiante. Ejemplo: Epson LX-350 (9 agujas, hasta 5 copias).

## Datos

- Base de datos: `%LOCALAPPDATA%\FacturacionFerreteria\facturacion.db` (se ve en Configuración > Respaldos).
- Respaldos diarios: `...\FacturacionFerreteria\respaldos\`. Registro de errores: `...\registros\`.
- Llevar con regularidad un respaldo a USB o a la nube (*Descargar respaldo ahora*).
- Ejemplo para probar: importar `ejemplos/inventario-ejemplo.csv` desde Productos.

## Instalar en la laptop de la ferretería

1. `.\publicar.ps1` (ejecuta las pruebas y genera `publicar\Facturacion.exe`, ~63 MB, no requiere instalar .NET).
2. Copiar la carpeta a la laptop (por ejemplo `C:\Facturacion`) y crear un acceso directo a `Facturacion.exe`.
3. Primera vez: revisar **Configuración** (datos del negocio, siguiente número del talonario, impresora) y calibrar.

## Pendiente de confirmar con el cliente

- Los datos del negocio y el rango de numeración se cargaron leyendo la foto de la factura: **verificarlos**.
- Calibrar el formato con un formulario real y la impresora definitiva (las medidas salen de una foto).
- Si una factura ya impresa se modifica, el formulario físico original ya tiene los datos viejos: definir con el
  cliente si se reimprime sobre ese mismo número o se anula y se emite otra (la DGI suele exigir lo segundo).

## Ideas para siguientes versiones

Ventas al crédito y cuentas por cobrar · cotizaciones/proformas · tipo de cambio y cobro en dólares ·
usuarios con PIN para anular o modificar · reporte de utilidad (requiere registrar costo) ·
facturas en espera para atender a dos clientes a la vez.

---
`prototipo/` conserva el diseño HTML original de referencia (no forma parte de la aplicación).
