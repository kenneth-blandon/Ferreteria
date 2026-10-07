using Facturacion.Core.Comun;
using Facturacion.Core.Importacion;
using Facturacion.Core.Inventario;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>/api/inventario: ajustes, alertas, importación y exportación.</summary>
public static class InventarioEndpoints
{
    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapearInventario(this IEndpointRouteBuilder app)
    {
        var inventario = app.MapGroup("/api/inventario");

        inventario.MapGet("/alertas", (ServicioInventario s) => s.Alertas());
        inventario.MapPost("/{productoId:long}/ajuste", (ServicioInventario s, long productoId, SolicitudAjuste datos) =>
            s.Ajustar(productoId, datos));
        inventario.MapGet("/{productoId:long}/movimientos", (ServicioInventario s, long productoId, int? limite) =>
            s.Movimientos(productoId, limite ?? 100));

        inventario.MapPost("/importar", async (ServicioImportacion s, IFormFile? archivo) =>
        {
            if (archivo is null || archivo.Length == 0)
                throw new ReglaNegocioException("Seleccione un archivo Excel (.xlsx) o CSV.");
            using var memoria = new MemoryStream();
            await archivo.CopyToAsync(memoria);
            memoria.Position = 0;
            return s.Importar(memoria, archivo.FileName);
        }).DisableAntiforgery();

        inventario.MapGet("/exportar", (ServicioImportacion s, TimeProvider reloj) =>
            Results.File(s.Exportar(), TipoExcel, $"inventario-{reloj.GetLocalNow():yyyy-MM-dd}.xlsx"));

        inventario.MapGet("/plantilla", (ServicioImportacion s) =>
            Results.File(s.Plantilla(), TipoExcel, "plantilla-productos.xlsx"));
    }
}
