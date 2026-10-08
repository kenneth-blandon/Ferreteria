using Facturacion.Core.Catalogo;
using Facturacion.Core.Clientes;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using Facturacion.Core.Importacion;
using Facturacion.Core.Inventario;
using Facturacion.Core.Reportes;
using Facturacion.Datos;
using Facturacion.Datos.Archivos;
using Facturacion.Datos.Repositorios;
using Facturacion.Datos.Respaldos;
using Microsoft.Data.Sqlite;

namespace Facturacion.Tests;

/// <summary>Reloj controlable para pruebas.</summary>
public sealed class RelojFijo(DateTime ahora) : TimeProvider
{
    public DateTime Ahora { get; set; } = ahora;
    public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Base de datos SQLite temporal con todos los servicios conectados.</summary>
public sealed class Entorno : IDisposable
{
    public Entorno()
    {
        Carpeta = Path.Combine(Path.GetTempPath(), "facturacion-pruebas", Guid.NewGuid().ToString("N"));
        Db = new ConexionSqlite(Path.Combine(Carpeta, "prueba.db"));
        Migraciones.Aplicar(Db);

        Reloj = new RelojFijo(new DateTime(2026, 10, 6, 10, 30, 0));
        Productos = new ProductoRepositorio(Db);
        Categorias = new CategoriaRepositorio(Db);
        Facturas = new FacturaRepositorio(Db);
        var clientes = new ClienteRepositorio(Db);

        Configuracion = new ServicioConfiguracion(new ConfiguracionRepositorio(Db), Facturas);
        Catalogo = new ServicioCatalogo(Productos, Categorias, new UnidadRepositorio(Db), Reloj);
        Clientes = new ServicioClientes(clientes, Reloj);
        Inventario = new ServicioInventario(new InventarioRepositorio(Db), Productos, Reloj);
        Facturacion = new ServicioFacturacion(Facturas, Productos, clientes, Configuracion, Reloj);
        Reportes = new ServicioReportes(new ReporteRepositorio(Db), Facturas);
        Importacion = new ServicioImportacion(Productos, [new LectorExcel(), new LectorCsv()], new ExportadorExcel(), Reloj);
        Respaldo = new ServicioRespaldo(Db, Path.Combine(Carpeta, "respaldos"), Reloj);
    }

    public string Carpeta { get; }
    public ConexionSqlite Db { get; }
    public RelojFijo Reloj { get; }
    public ProductoRepositorio Productos { get; }
    public CategoriaRepositorio Categorias { get; }
    public FacturaRepositorio Facturas { get; }
    public ServicioConfiguracion Configuracion { get; }
    public ServicioCatalogo Catalogo { get; }
    public ServicioClientes Clientes { get; }
    public ServicioInventario Inventario { get; }
    public ServicioFacturacion Facturacion { get; }
    public ServicioReportes Reportes { get; }
    public ServicioImportacion Importacion { get; }
    public ServicioRespaldo Respaldo { get; }

    public Producto CrearProducto(string codigo, string nombre, decimal precio, decimal stock = 10, decimal minimo = 0, long? categoriaId = null) =>
        Catalogo.Crear(new DatosProducto(codigo, null, nombre, categoriaId, "UND", precio, minimo, stock));

    public static SolicitudFactura Venta(params LineaSolicitud[] lineas) =>
        new(null, lineas, MetodoPago.Efectivo, null, null);

    public static LineaSolicitud Linea(Producto producto, decimal cantidad) =>
        new(producto.Id, null, null, cantidad, producto.Precio);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Carpeta, recursive: true); } catch (IOException) { }
    }
}
