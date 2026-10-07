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
/// /imprimir/...: documentos HTML listos para imprimir. La ventana de escritorio los carga
/// en un navegador oculto y los envía directo a la impresora configurada.
/// </summary>
public static class ImpresionEndpoints
{
    private const string TipoHtml = "text/html; charset=utf-8";

    public static void MapearImpresion(this IEndpointRouteBuilder app)
    {
        var imprimir = app.MapGroup("/imprimir");

        imprimir.MapGet("/factura/{id:long}", (long id, string? formato, bool? reimpresion,
            ServicioFacturacion facturas, ServicioConfiguracion configuracion) =>
        {
            var factura = facturas.Obtener(id);
            var ajustes = configuracion.Obtener();
            var esReimpresion = reimpresion ?? false;
            var html = (LeerFormato(formato) ?? ajustes.Impresion.Formato) switch
            {
                FormatoImpresion.Ticket => PlantillaTicket.Generar(factura, ajustes, esReimpresion),
                FormatoImpresion.Preimpreso => PlantillaPreimpreso.Generar(factura, ajustes.Impresion),
                _ => PlantillaCarta.Generar(factura, ajustes, esReimpresion)
            };
            return Results.Content(html, TipoHtml);
        });

        imprimir.MapGet("/cierre", (DateOnly? fecha, string? formato, ServicioReportes reportes,
            ServicioConfiguracion configuracion, TimeProvider reloj) =>
        {
            var ahora = reloj.AhoraLocal();
            var resumen = reportes.Cierre(fecha ?? DateOnly.FromDateTime(ahora));
            var ajustes = configuracion.Obtener();
            // El cierre no tiene versión preimpresa: se usa la hoja carta.
            var formatoCierre = LeerFormato(formato) ?? ajustes.Impresion.Formato;
            return Results.Content(PlantillaCierre.Generar(resumen, ajustes, formatoCierre, ahora), TipoHtml);
        });

        // Hoja de prueba para alinear el formato preimpreso con el talonario.
        imprimir.MapGet("/calibracion", (bool? cuadricula, ServicioConfiguracion configuracion, TimeProvider reloj) =>
        {
            var ejemplo = PlantillaPreimpreso.FacturaDeEjemplo(reloj.AhoraLocal());
            return Results.Content(
                PlantillaPreimpreso.Generar(ejemplo, configuracion.ObtenerImpresion(), cuadricula ?? true), TipoHtml);
        });
    }

    private static FormatoImpresion? LeerFormato(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato)) return null;
        return Enum.TryParse<FormatoImpresion>(formato, ignoreCase: true, out var valor)
            ? valor
            : throw new ReglaNegocioException("Formato de impresión no válido.");
    }
}
