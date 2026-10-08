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
using Facturacion.Web.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Web;

/// <summary>
/// Punto único donde se conectan las piezas: interfaces del dominio (Core) con
/// sus implementaciones (Datos). Todos los servicios son sin estado, por eso son singleton.
/// </summary>
public static class Composicion
{
    public static void Registrar(IServiceCollection servicios, string carpetaDatos, string nombreHost)
    {
        var archivoBase = Path.Combine(carpetaDatos, "facturacion.db");
        var carpetaRespaldos = Path.Combine(carpetaDatos, "respaldos");

        servicios.AddSingleton(TimeProvider.System);
        servicios.AddSingleton(new ConexionSqlite(archivoBase));
        servicios.AddSingleton(new InfoSistema(nombreHost, carpetaDatos, archivoBase, carpetaRespaldos));

        // Datos
        servicios.AddSingleton<IConfiguracionRepositorio, ConfiguracionRepositorio>();
        servicios.AddSingleton<IProductoRepositorio, ProductoRepositorio>();
        servicios.AddSingleton<ICategoriaRepositorio, CategoriaRepositorio>();
        servicios.AddSingleton<IUnidadRepositorio, UnidadRepositorio>();
        servicios.AddSingleton<IClienteRepositorio, ClienteRepositorio>();
        servicios.AddSingleton<IFacturaRepositorio, FacturaRepositorio>();
        servicios.AddSingleton<IInventarioRepositorio, InventarioRepositorio>();
        servicios.AddSingleton<IReporteRepositorio, ReporteRepositorio>();
        servicios.AddSingleton<ILectorArchivo, LectorExcel>();
        servicios.AddSingleton<ILectorArchivo, LectorCsv>();
        servicios.AddSingleton<IExportadorInventario, ExportadorExcel>();
        servicios.AddSingleton(sp => new ServicioRespaldo(
            sp.GetRequiredService<ConexionSqlite>(), carpetaRespaldos, sp.GetRequiredService<TimeProvider>()));

        // Reglas de negocio
        servicios.AddSingleton<ServicioConfiguracion>();
        servicios.AddSingleton<ServicioCatalogo>();
        servicios.AddSingleton<ServicioClientes>();
        servicios.AddSingleton<ServicioInventario>();
        servicios.AddSingleton<ServicioFacturacion>();
        servicios.AddSingleton<ServicioReportes>();
        servicios.AddSingleton<ServicioImportacion>();

        // Por defecto no hay listado de impresoras; el host de escritorio registra el de Windows.
        servicios.AddSingleton<IImpresoras, SinImpresoras>();
    }
}

/// <summary>Datos de instalación que se muestran en Configuración.</summary>
public sealed record InfoSistema(string Host, string CarpetaDatos, string ArchivoBaseDatos, string CarpetaRespaldos)
{
    public string Version { get; } = typeof(InfoSistema).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
