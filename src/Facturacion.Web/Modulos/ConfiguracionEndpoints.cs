using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Datos.Respaldos;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>/api/configuracion, /api/impresoras, /api/sistema y /api/respaldo</summary>
public static class ConfiguracionEndpoints
{
    public static void MapearConfiguracion(this IEndpointRouteBuilder app)
    {
        var configuracion = app.MapGroup("/api/configuracion");
        configuracion.MapGet("/", (ServicioConfiguracion s) => s.Obtener());
        configuracion.MapPut("/negocio", (ServicioConfiguracion s, DatosNegocio datos) => s.GuardarNegocio(datos));
        configuracion.MapPut("/facturacion", (ServicioConfiguracion s, AjustesFacturacion datos) => s.GuardarFacturacion(datos));
        configuracion.MapPut("/impresion", (ServicioConfiguracion s, AjustesImpresion datos) => s.GuardarImpresion(datos));
        configuracion.MapGet("/plantilla-preimpresa", () => new PlantillaPreimpresa());

        app.MapGet("/api/impresoras", (IImpresoras impresoras) =>
            new { impresoras = impresoras.Listar(), predeterminada = impresoras.Predeterminada() });

        app.MapGet("/api/sistema", (InfoSistema info) => info);

        app.MapGet("/api/respaldo", (ServicioRespaldo respaldo, TimeProvider reloj) =>
        {
            var temporal = Path.Combine(Path.GetTempPath(), $"respaldo-{Guid.NewGuid():N}.db");
            try
            {
                respaldo.CrearEn(temporal);
                return Results.File(File.ReadAllBytes(temporal), "application/octet-stream",
                    $"respaldo-facturacion-{reloj.GetLocalNow():yyyy-MM-dd-HHmm}.db");
            }
            finally
            {
                File.Delete(temporal);
            }
        });

        app.MapPost("/api/respaldo/restaurar", async (ServicioRespaldo respaldo, IFormFile? archivo) =>
        {
            if (archivo is null || archivo.Length == 0)
                throw new ReglaNegocioException("Seleccione el archivo de respaldo (.db).");

            var temporal = Path.Combine(Path.GetTempPath(), $"restaurar-{Guid.NewGuid():N}.db");
            try
            {
                await using (var destino = File.Create(temporal))
                    await archivo.CopyToAsync(destino);
                var copiaPrevia = respaldo.Restaurar(temporal);
                return new { mensaje = "Datos restaurados correctamente.", copiaPrevia };
            }
            finally
            {
                File.Delete(temporal);
            }
        }).DisableAntiforgery();
    }
}
