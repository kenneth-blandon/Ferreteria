using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using Facturacion.Core.Reportes;
using Facturacion.Web.Impresion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>
/// /imprimir/...: documentos HTML. La factura se imprime SOLO sobre el talonario preimpreso;
/// la hoja carta es la vista previa (y el PDF). La ventana de escritorio los envía directo a la impresora.
/// </summary>
public static class ImpresionEndpoints
{
    private const string TipoHtml = "text/html; charset=utf-8";

    public static void MapearImpresion(this IEndpointRouteBuilder app)
    {
        var imprimir = app.MapGroup("/imprimir");

        // Lo que se envía a la impresora de matriz de punto: solo los datos, alineados al formulario.
        imprimir.MapGet("/factura/{id:long}", (long id, ServicioFacturacion facturas, ServicioConfiguracion configuracion) =>
            Results.Content(PlantillaPreimpreso.Generar(facturas.Obtener(id), configuracion.ObtenerImpresion()), TipoHtml));

        // Vista previa: el talonario completo con los datos, para ver cómo quedará la factura.
        imprimir.MapGet("/factura/{id:long}/vista", (long id, bool? reimpresion, ServicioFacturacion facturas,
            ServicioConfiguracion configuracion) =>
            Results.Content(PlantillaCarta.Generar(facturas.Obtener(id), configuracion.Obtener(), reimpresion ?? false), TipoHtml));

        imprimir.MapGet("/cierre", (DateOnly? fecha, ServicioReportes reportes, ServicioConfiguracion configuracion, TimeProvider reloj) =>
        {
            var ahora = reloj.AhoraLocal();
            var resumen = reportes.Cierre(fecha ?? DateOnly.FromDateTime(ahora));
            return Results.Content(PlantillaCierre.Generar(resumen, configuracion.Obtener(), ahora), TipoHtml);
        });

        // Hoja de prueba para alinear la plantilla con el talonario (llena todas las filas).
        imprimir.MapGet("/calibracion", (bool? cuadricula, ServicioConfiguracion configuracion, TimeProvider reloj) =>
        {
            var impresion = configuracion.ObtenerImpresion();
            var ejemplo = PlantillaPreimpreso.FacturaDeEjemplo(reloj.AhoraLocal(), impresion.Plantilla.Filas.Maximo);
            return Results.Content(PlantillaPreimpreso.Generar(ejemplo, impresion, cuadricula ?? true), TipoHtml);
        });
    }
}
