using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Facturacion.Core.Comun;

/// <summary>
/// Normalización de texto. Todo lo que se guarda pasa por aquí para evitar duplicados
/// como "Tubo PVC", "TUBO  PVC " y "tubo pvc".
/// </summary>
public static partial class Texto
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    /// <summary>Quita espacios al inicio/final y deja un solo espacio entre palabras.</summary>
    public static string Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "" : Espacios().Replace(valor.Trim(), " ");

    /// <summary>Limpia y convierte a mayúsculas (conserva tildes y Ñ).</summary>
    public static string Mayusculas(string? valor) => Limpiar(valor).ToUpperInvariant();

    /// <summary>Igual que <see cref="Mayusculas"/>, pero devuelve null si queda vacío.</summary>
    public static string? MayusculasONulo(string? valor)
    {
        var resultado = Mayusculas(valor);
        return resultado.Length == 0 ? null : resultado;
    }

    /// <summary>Limpia y devuelve null si queda vacío (sin cambiar mayúsculas).</summary>
    public static string? LimpiarONulo(string? valor)
    {
        var resultado = Limpiar(valor);
        return resultado.Length == 0 ? null : resultado;
    }

    /// <summary>Elimina tildes y diéresis: "TUBERÍA" → "TUBERIA", "NIÑO" → "NINO".</summary>
    public static string SinAcentos(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                resultado.Append(caracter);
        }
        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Texto indexable para búsquedas sin importar mayúsculas ni tildes.</summary>
    public static string ClaveBusqueda(params string?[] partes) =>
        SinAcentos(Mayusculas(string.Join(' ', partes.Where(p => !string.IsNullOrWhiteSpace(p)))));

    /// <summary>Divide una consulta en términos normalizados: "tubo  3/4" → ["TUBO", "3/4"].</summary>
    public static IReadOnlyList<string> Terminos(string? consulta) =>
        ClaveBusqueda(consulta).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Recorta un texto a un largo máximo.</summary>
    public static string Recortar(string valor, int largoMaximo) =>
        valor.Length <= largoMaximo ? valor : valor[..largoMaximo];
}
