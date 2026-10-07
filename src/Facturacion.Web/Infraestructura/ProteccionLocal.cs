using Microsoft.AspNetCore.Http;

namespace Facturacion.Web.Infraestructura;

/// <summary>
/// El servidor solo escucha en 127.0.0.1, pero una página web maliciosa abierta en otro navegador
/// del mismo equipo podría enviarle formularios. Toda petición que modifica datos debe traer el
/// encabezado <see cref="Encabezado"/>: un sitio ajeno no puede agregarlo sin permiso CORS (que nunca se da).
/// </summary>
public sealed class ProteccionLocal(RequestDelegate siguiente)
{
    public const string Encabezado = "X-Facturacion";

    public async Task InvokeAsync(HttpContext contexto)
    {
        var solicitud = contexto.Request;
        var modifica = !HttpMethods.IsGet(solicitud.Method) && !HttpMethods.IsHead(solicitud.Method);
        if (modifica && solicitud.Path.StartsWithSegments("/api") && solicitud.Headers[Encabezado] != "1")
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await contexto.Response.WriteAsJsonAsync(new { error = "Solicitud no permitida." });
            return;
        }
        await siguiente(contexto);
    }
}
