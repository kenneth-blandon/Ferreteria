using System.Globalization;
using System.Net;

namespace Facturacion.Web.Impresion;

/// <summary>Utilidades para generar los documentos HTML de impresión.</summary>
internal static class Html
{
    /// <summary>Codifica texto del usuario para insertarlo en HTML de forma segura.</summary>
    public static string E(string? texto) => WebUtility.HtmlEncode(texto ?? "");

    /// <summary>Medida en milímetros con punto decimal (CSS).</summary>
    public static string Mm(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture) + "mm";

    public static string Pt(decimal valor) => valor.ToString("0.#", CultureInfo.InvariantCulture) + "pt";

    /// <summary>Texto con saltos de línea → líneas HTML.</summary>
    public static string Multilinea(string? texto) =>
        string.Join("<br>", (texto ?? "").Split('\n').Select(l => E(l.TrimEnd('\r'))));

    /// <summary>
    /// Estilos comunes: sin márgenes de página (la plantilla controla las posiciones)
    /// y en pantalla se ve como una hoja sobre fondo gris.
    /// </summary>
    public const string EstilosBase = """
        @page { margin: 0; }
        * { box-sizing: border-box; }
        html, body { margin: 0; padding: 0; }
        body { color: #000; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        @media screen {
          body { background: #d4d8de; padding: 12px 0; }
          .hoja { background: #fff; margin: 0 auto; box-shadow: 0 2px 14px rgba(0,0,0,.18); }
        }
        """;

    public static string Documento(string titulo, string estilos, string cuerpo) => $$"""
        <!doctype html>
        <html lang="es">
        <head>
        <meta charset="utf-8">
        <title>{{E(titulo)}}</title>
        <style>
        {{EstilosBase}}
        {{estilos}}
        </style>
        </head>
        <body>
        {{cuerpo}}
        </body>
        </html>
        """;
}
