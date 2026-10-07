using Facturacion.Core.Comun;
using Facturacion.Core.Facturas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>/api/facturas: emitir, consultar y anular.</summary>
public static class FacturasEndpoints
{
    public sealed record MotivoSolicitud(string? Motivo);

    public static void MapearFacturas(this IEndpointRouteBuilder app)
    {
        var facturas = app.MapGroup("/api/facturas");

        facturas.MapGet("/", (ServicioFacturacion s, TimeProvider reloj, DateOnly? desde, DateOnly? hasta, string? texto, string? estado) =>
        {
            var hoy = DateOnly.FromDateTime(reloj.AhoraLocal());
            var inicio = desde ?? hoy;
            return s.Listar(new FiltroFacturas(inicio, hasta ?? inicio, texto, LeerEstado(estado)));
        });

        facturas.MapGet("/proximo-numero", (ServicioFacturacion s) => s.ProximoNumero());
        facturas.MapGet("/{id:long}", (ServicioFacturacion s, long id) => s.Obtener(id));
        facturas.MapPost("/", (ServicioFacturacion s, SolicitudFactura datos) => s.Emitir(datos));
        facturas.MapPost("/{id:long}/anular", (ServicioFacturacion s, long id, MotivoSolicitud datos) => s.Anular(id, datos.Motivo));
    }

    private static EstadoFactura? LeerEstado(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado)) return null;
        return Enum.TryParse<EstadoFactura>(estado, ignoreCase: true, out var valor)
            ? valor
            : throw new ReglaNegocioException("Estado de factura no válido.");
    }
}
