# Sistema de facturación · Ferretería

Aplicación de escritorio para facturar rápido en mostrador: C# (.NET 10) en el servidor local,
interfaz en HTML/CSS/JavaScript y base de datos SQLite en un solo archivo. Funciona **sin internet**.

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
| Catálogo | `Catalogo/` | `ProductoRepositorio`, `CategoriaRepositorio` | `CatalogoEndpoints` | `productos.js` |
| Inventario | `Inventario/`, `Importacion/` | `InventarioRepositorio`, `Archivos/` | `InventarioEndpoints` | `productos.js` |
| Clientes | `Clientes/` | `ClienteRepositorio` | `ClientesEndpoints` | `clientes.js` |
| Cierre de caja | `Reportes/` | `ReporteRepositorio` | `ReportesEndpoints` | `cierre.js` |
| Configuración | `Configuracion/` | `ConfiguracionRepositorio`, `Respaldos/` | `ConfiguracionEndpoints` | `configuracion.js` |
| Impresión | — | — | `ImpresionEndpoints` + `Web/Impresion/` | `impresion.js` |

La conexión entre interfaces e implementaciones está en un solo lugar: `Web/Composicion.cs`.

**Agregar un módulo nuevo:** modelos y servicio en `Core/<Modulo>`, repositorio en `Datos/Repositorios`,
registro en `Composicion.cs`, `Web/Modulos/<Modulo>Endpoints.cs` (y llamarlo en `AplicacionWeb.cs`),
`wwwroot/js/modulos/<modulo>.js` (exporta `montar`) y su ruta en `wwwroot/js/app.js`.
**Cambiar la base de datos:** agregar un paso nuevo al final de `Datos/Migraciones.cs` (nunca editar los anteriores).

## Requisitos cubiertos

- **Facturación acelerada:** buscador con autocompletado desde 2 caracteres (máximo 20 resultados), por código,
  nombre, categoría o código de barras, sin importar tildes ni mayúsculas. Lector de código de barras (código + Enter).
  `5*código` agrega 5 unidades; un producto repetido suma cantidad. Precio editable por línea y artículos libres (F4).
- **Cliente:** Consumidor Final por defecto; autocompletado de clientes guardados y opción "Guardar cliente".
- **Totales:** subtotal, IVA configurable (desactivado por defecto: el talonario es de **Cuota Fija**), total en letras
  y vuelto con botones de billetes. El servidor recalcula todo antes de guardar.
- **Numeración:** serie y siguiente número sincronizados con el talonario físico, rango autorizado (avisa cuando
  quedan 50 y bloquea al agotarse) y nunca se repite un número.
- **Catálogo:** alta/edición, cambio rápido de precio (clic en el precio), categorías, stock mínimo con alertas,
  ajustes de existencias (entrada, salida, conteo) con historial de movimientos.
- **Importar/Exportar:** Excel (.xlsx) o CSV (`,` o `;`, UTF-8 o ANSI). UPSERT por código en una sola transacción:
  no duplica, actualiza precios y existencias; las celdas vacías conservan el valor actual; informa errores por fila.
  La exportación sirve para conteo físico (columna "Conteo físico") y se puede volver a importar.
- **Impresión:** hoja carta (igual al talonario), ticket 80/58 mm y **preimpreso** (solo los datos, alineados sobre
  el formulario) con hoja de calibración milimétrica. Reimpresión marcada como tal y PDF.
- **Historial y anulación:** facturas del día o por rango; anular exige motivo, devuelve existencias y la factura
  queda registrada (marca de agua ANULADA).
- **Cierre de caja:** total por forma de pago, anuladas, numeración usada, productos más vendidos; imprimible.
- **Respaldos:** copia automática diaria (30 días), descarga manual y restauración (guarda antes una copia).

### Atajos de teclado

| Tecla | Acción | Tecla | Acción |
|---|---|---|---|
| F1 | Buscar producto | F8 | Efectivo recibido |
| F2 | Cliente | F10 (o F9, Ctrl+Enter) | Cobrar e imprimir |
| F4 | Artículo libre | Esc | Cerrar lista / limpiar factura |
| Enter | Agregar / confirmar | Alt+1 … Alt+6 | Cambiar de módulo |

La factura en curso se guarda sola: si se cierra la ventana, al volver sigue ahí.

## Impresión y calibración

En la versión de escritorio se imprime **directo, sin cuadro de diálogo**, en la impresora elegida en
Configuración > Impresión (o la predeterminada de Windows). Formato preimpreso:

1. Configuración > Impresión > *Ver hoja de prueba* (muestra una cuadrícula en milímetros).
2. Imprima la hoja de prueba sobre un formulario del talonario.
3. Si todo sale corrido, ajuste *Desplazamiento X/Y*; si un campo puntual no calza, edite *Posiciones avanzadas*.

Las posiciones iniciales son estimadas a partir de la foto (hoja carta). Deben calibrarse con el talonario real.

### ¿Qué impresora comprar?

- **Si se sigue usando el talonario autorizado (original + copias):** impresora de **matriz de punto**, la única
  que imprime a través del papel autocopiante. Ejemplo: Epson LX-350 (9 agujas, hasta 5 copias). Formato: *Preimpreso*.
- **Si se pasa a papel en blanco** (factura computarizada): **térmica de 80 mm** (Epson TM-T20 o similares,
  rápida y sin tinta) para ticket, o **inyección de tanque** (Epson EcoTank) para hoja carta.
  Antes de este cambio, confirme con el contador o la DGI los requisitos de autorización del sistema de facturación.

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

- Los datos del negocio (nombre, RUC, teléfonos, correo, dirección) y el rango de numeración se cargaron leyendo la
  foto de la factura: **verificarlos** en Configuración antes de usar el sistema.
- Calibrar el formato preimpreso con un formulario real y probar la impresora definitiva.

## Ideas para siguientes versiones

Ventas al crédito y cuentas por cobrar · cotizaciones/proformas · tipo de cambio y cobro en dólares ·
PIN para anular o cambiar precios · impresión ESC/POS directa (corte de papel, gaveta de dinero) · reporte de
utilidad (requiere registrar costo) · facturas en espera para atender a dos clientes a la vez.

