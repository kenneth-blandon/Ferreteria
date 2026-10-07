using System.Globalization;
using System.Text.Json.Serialization;
using Facturacion.Datos;
using Facturacion.Datos.Respaldos;
using Facturacion.Web.Infraestructura;
using Facturacion.Web.Modulos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Facturacion.Web;

public sealed class OpcionesAplicacion
{
    /// <summary>Dirección local del servidor. Se puede cambiar con la clave "Servidor:Url".</summary>
    public string Url { get; set; } = "http://127.0.0.1:5080";

    /// <summary>Carpeta de datos. Se puede cambiar con la clave "Datos:Carpeta".</summary>
    public string? CarpetaDatos { get; set; }

    /// <summary>"Escritorio" o "Servidor": se muestra en Configuración.</summary>
    public string NombreHost { get; set; } = "Servidor";

    /// <summary>Servicios propios del host, por ejemplo el listado de impresoras de Windows.</summary>
    public Action<IServiceCollection>? ServiciosDelHost { get; set; }
}

/// <summary>
/// Arma la aplicación web completa (API + interfaz + impresión). La usan los dos hosts:
/// la ventana de escritorio y el servidor de consola.
/// </summary>
public static class AplicacionWeb
{
    public static string CarpetaDatosPredeterminada =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FacturacionFerreteria");

    public static WebApplication Crear(string[] args, OpcionesAplicacion opciones)
    {
        // Fechas y textos siempre en español de Nicaragua, sin importar el idioma de Windows.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-NI");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        var url = builder.Configuration["Servidor:Url"] is { Length: > 0 } urlConfigurada ? urlConfigurada : opciones.Url;
        builder.WebHost.UseUrls(url);
        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 200 * 1024 * 1024);

        var carpetaDatos = builder.Configuration["Datos:Carpeta"] is { Length: > 0 } carpeta
            ? Path.GetFullPath(carpeta)
            : opciones.CarpetaDatos ?? CarpetaDatosPredeterminada;

        builder.Logging.AddProvider(new RegistroArchivoProvider(Path.Combine(carpetaDatos, "registros")));

        Composicion.Registrar(builder.Services, carpetaDatos, opciones.NombreHost);
        opciones.ServiciosDelHost?.Invoke(builder.Services);

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        });
        builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
        builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 200 * 1024 * 1024);
        // Solo se responde a direcciones locales (evita ataques de "DNS rebinding").
        builder.Services.PostConfigure<HostFilteringOptions>(o => o.AllowedHosts = ["127.0.0.1", "localhost"]);

        var app = builder.Build();
        Inicializar(app);

        app.UseMiddleware<ManejoErrores>();
        app.UseMiddleware<ProteccionLocal>();
        ArchivosWeb.Usar(app);

        app.MapearCatalogo();
        app.MapearInventario();
        app.MapearClientes();
        app.MapearFacturas();
        app.MapearReportes();
        app.MapearConfiguracion();
        app.MapearImpresion();
        return app;
    }

    /// <summary>Crea/actualiza la base de datos y guarda el respaldo del día.</summary>
    private static void Inicializar(WebApplication app)
    {
        Migraciones.Aplicar(app.Services.GetRequiredService<ConexionSqlite>());
        try
        {
            app.Services.GetRequiredService<ServicioRespaldo>().RespaldoDiario();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "No se pudo crear el respaldo diario.");
        }
    }
}
