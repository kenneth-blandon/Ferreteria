using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Facturacion.Web.Infraestructura;

/// <summary>
/// Sirve la interfaz HTML. En producción sale de los recursos incrustados en el .dll;
/// en desarrollo, de la carpeta src/Facturacion.Web/wwwroot para ver los cambios sin recompilar.
/// </summary>
public static class ArchivosWeb
{
    public static void Usar(WebApplication app)
    {
        var proveedor = Resolver(app.Environment);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = proveedor });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = proveedor,
            // Siempre revalidar: tras actualizar la aplicación no queda JavaScript viejo en caché.
            OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "no-cache"
        });
    }

    private static IFileProvider Resolver(IWebHostEnvironment entorno)
    {
        if (entorno.IsDevelopment() && BuscarCarpetaFuente() is { } carpeta)
            return new PhysicalFileProvider(carpeta);
        return new ManifestEmbeddedFileProvider(typeof(ArchivosWeb).Assembly, "wwwroot");
    }

    private static string? BuscarCarpetaFuente()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidata = Path.Combine(dir.FullName, "src", "Facturacion.Web", "wwwroot");
            if (File.Exists(Path.Combine(candidata, "index.html"))) return candidata;
        }
        return null;
    }
}
