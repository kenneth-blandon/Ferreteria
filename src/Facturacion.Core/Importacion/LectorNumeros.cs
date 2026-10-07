using System.Globalization;

namespace Facturacion.Core.Importacion;

/// <summary>
/// Interpreta números escritos de distintas formas: "1250.5", "1,250.50", "1.250,50",
/// "C$ 3,890.00", "12,5". Si hay punto y coma, el último separador es el decimal.
/// </summary>
public static class LectorNumeros
{
    public static bool Intentar(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var s = texto.Trim()
            .Replace("C$", "", StringComparison.OrdinalIgnoreCase)
            .Replace("$", "")
            .Replace(" ", "")
            .Replace(" ", "");
        if (s.Length == 0) return false;

        var coma = s.LastIndexOf(',');
        var punto = s.LastIndexOf('.');

        if (coma >= 0 && punto >= 0)
        {
            // El separador que aparece último es el decimal.
            s = coma > punto ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        }
        else if (coma >= 0)
        {
            var decimalesTrasComa = s.Length - coma - 1;
            var unaSolaComa = s.IndexOf(',') == coma;
            var parteEntera = s[..coma].TrimStart('-', '+');
            // "12,5", "12,50" o "0,250" → decimal. "1,250" o "1,250,000" → miles.
            var esDecimal = unaSolaComa && (decimalesTrasComa != 3 || parteEntera is "" or "0");
            s = esDecimal ? s.Replace(',', '.') : s.Replace(",", "");
        }
        else if (punto >= 0 && s.IndexOf('.') != punto)
        {
            // "1.250.000" → miles.
            s = s.Replace(".", "");
        }

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out valor);
    }
}
