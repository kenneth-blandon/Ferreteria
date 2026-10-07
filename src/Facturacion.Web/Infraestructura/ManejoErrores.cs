using Facturacion.Core.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Facturacion.Web.Infraestructura;

/// <summary>
/// Convierte excepciones en respuestas JSON { "error": "mensaje" } que la interfaz muestra al usuario.
/// </summary>
public sealed class ManejoErrores(RequestDelegate siguiente, ILogger<ManejoErrores> registro)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (ReglaNegocioException ex)
        {
            await Responder(contexto, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (NoEncontradoException ex)
        {
            await Responder(contexto, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            registro.LogWarning(ex, "Solicitud inválida en {Ruta}", contexto.Request.Path);
            await Responder(contexto, StatusCodes.Status400BadRequest, "Los datos enviados no son válidos. Revise los campos e intente de nuevo.");
        }
        catch (Exception ex)
        {
            registro.LogError(ex, "Error no controlado en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);
            await Responder(contexto, StatusCodes.Status500InternalServerError,
                "Ocurrió un error inesperado. Intente de nuevo; si continúa, revise el registro de errores en la carpeta de datos.");
        }
    }

    private static Task Responder(HttpContext contexto, int estado, string mensaje)
    {
        if (contexto.Response.HasStarted) return Task.CompletedTask;
        contexto.Response.Clear();
        contexto.Response.StatusCode = estado;
        return contexto.Response.WriteAsJsonAsync(new { error = mensaje });
    }
}
