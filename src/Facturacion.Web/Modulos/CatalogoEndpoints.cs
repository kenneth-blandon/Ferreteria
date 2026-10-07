using Facturacion.Core.Catalogo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Facturacion.Web.Modulos;

/// <summary>/api/productos y /api/categorias</summary>
public static class CatalogoEndpoints
{
    public sealed record NombreSolicitud(string? Nombre);
    public sealed record PrecioSolicitud(decimal Precio);

    public static void MapearCatalogo(this IEndpointRouteBuilder app)
    {
        var categorias = app.MapGroup("/api/categorias");
        categorias.MapGet("/", (ServicioCatalogo s) => s.Categorias());
        categorias.MapPost("/", (ServicioCatalogo s, NombreSolicitud datos) => s.CrearCategoria(datos.Nombre));
        categorias.MapPut("/{id:long}", (ServicioCatalogo s, long id, NombreSolicitud datos) => s.RenombrarCategoria(id, datos.Nombre));
        categorias.MapDelete("/{id:long}", (ServicioCatalogo s, long id) =>
        {
            s.EliminarCategoria(id);
            return Results.NoContent();
        });

        var productos = app.MapGroup("/api/productos");
        productos.MapGet("/", (ServicioCatalogo s, string? texto, long? categoriaId, bool? bajoStock, int? pagina, int? tamano) =>
            s.Listar(new FiltroProductos(texto, categoriaId, bajoStock ?? false, pagina ?? 1, tamano ?? 50)));

        // Autocompletado del buscador de facturación (mínimo 2 caracteres, top 20).
        productos.MapGet("/buscar", (ServicioCatalogo s, string? q, int? limite) =>
            s.Buscar(q, limite ?? ServicioCatalogo.LimiteBusquedaPredeterminado));

        // Código interno o de barras exacto (lector de código de barras). Va por query porque los códigos pueden traer "/".
        productos.MapGet("/por-codigo", (ServicioCatalogo s, string? codigo) => s.ObtenerPorCodigo(codigo));

        productos.MapGet("/{id:long}", (ServicioCatalogo s, long id) => s.Obtener(id));
        productos.MapPost("/", (ServicioCatalogo s, DatosProducto datos) => s.Crear(datos));
        productos.MapPut("/{id:long}", (ServicioCatalogo s, long id, DatosProducto datos) => s.Actualizar(id, datos));
        productos.MapPatch("/{id:long}/precio", (ServicioCatalogo s, long id, PrecioSolicitud datos) => s.CambiarPrecio(id, datos.Precio));
        productos.MapDelete("/{id:long}", (ServicioCatalogo s, long id) =>
        {
            s.Eliminar(id);
            return Results.NoContent();
        });
    }
}
