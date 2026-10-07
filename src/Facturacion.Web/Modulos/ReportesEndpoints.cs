using Facturacion.Core.Comun;
using Facturacion.Core.Reportes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>/api/reportes</summary>
public static class ReportesEndpoints
{
    public static void MapearReportes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reportes/cierre", (ServicioReportes s, TimeProvider reloj, DateOnly? fecha) =>
            s.Cierre(fecha ?? DateOnly.FromDateTime(reloj.AhoraLocal())));
    }
}
