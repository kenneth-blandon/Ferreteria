using Facturacion.Core.Clientes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>/api/clientes</summary>
public static class ClientesEndpoints
{
    public static void MapearClientes(this IEndpointRouteBuilder app)
    {
        var clientes = app.MapGroup("/api/clientes");
        clientes.MapGet("/", (ServicioClientes s, string? texto, int? limite) => s.Buscar(texto, limite ?? 20));
        clientes.MapGet("/{id:long}", (ServicioClientes s, long id) => s.Obtener(id));
        clientes.MapPost("/", (ServicioClientes s, DatosCliente datos) => s.Crear(datos));
        clientes.MapPut("/{id:long}", (ServicioClientes s, long id, DatosCliente datos) => s.Actualizar(id, datos));
        clientes.MapDelete("/{id:long}", (ServicioClientes s, long id) =>
        {
            s.Eliminar(id);
            return Results.NoContent();
        });
    }
}
