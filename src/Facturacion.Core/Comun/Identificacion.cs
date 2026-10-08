using System.Globalization;
using System.Text.RegularExpressions;

namespace Facturacion.Core.Comun;

/// <summary>
/// Validación de teléfonos, cédulas y RUC de Nicaragua.
/// <list type="bullet">
/// <item>Teléfono: 8 dígitos (ej. 89704920).</item>
/// <item>Cédula: 000-000000-0000A → municipio, fecha de nacimiento (ddmmaa), consecutivo y letra de control.</item>
/// <item>RUC (DGI, Disposición Administrativa General 01-2013): 14 caracteres. Persona natural con cédula
/// (13 dígitos + letra), sin cédula (N), extranjero residente (R) o no residente (E) y persona jurídica (J).
/// No son válidos los que inician con cuatro ceros.</item>
/// </list>
/// </summary>
public static partial class Identificacion
{
    public const string EjemploTelefono = "89704920";
    public const string FormatoCedula = "000-000000-0000A";
    public const string EjemploRuc = "J0310000007669";

    /// <summary>Letras de control de la cédula: se excluyen I, Ñ, O y Z.</summary>
    private const string LetrasControl = "ABCDEFGHJKLMNPQRSTUVWXY";

    [GeneratedRegex(@"^(\d{3})-?(\d{6})-?(\d{4})([A-Z])$")]
    private static partial Regex PatronCedula();

    [GeneratedRegex(@"^[JNRE]\d{13}$")]
    private static partial Regex PatronRucConLetra();

    [GeneratedRegex(@"^\d{13}[A-Z]$")]
    private static partial Regex PatronRucNatural();

    [GeneratedRegex(@"^[\d\s().+-]+$")]
    private static partial Regex CaracteresTelefono();

    // ---------- Teléfonos ----------

    /// <summary>Teléfono de 8 dígitos. Acepta espacios, guiones y el prefijo +505. Vacío → null.</summary>
    public static string? Telefono(string? valor, string campo = "El teléfono")
    {
        var texto = Texto.Limpiar(valor);
        if (texto.Length == 0) return null;
        if (!CaracteresTelefono().IsMatch(texto))
            throw new ReglaNegocioException($"{campo} solo puede tener números (ej. {EjemploTelefono}).");

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length == 11 && digitos.StartsWith("505", StringComparison.Ordinal)) digitos = digitos[3..];
        if (digitos.Length != 8)
            throw new ReglaNegocioException($"{campo} debe tener 8 dígitos (ej. {EjemploTelefono}).");
        return digitos;
    }

    /// <summary>Uno o varios teléfonos separados por "/" (datos del negocio). Vacío → "".</summary>
    public static string Telefonos(string? valor)
    {
        var partes = (valor ?? "").Split(['/', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" / ", partes.Select(p => Telefono(p, $"El teléfono '{p}'")!));
    }

    /// <summary>Presentación para documentos: 89704920 → 8970-4920 (también en listas "a / b").</summary>
    public static string FormatoTelefono(string? telefonos) =>
        string.Join(" / ", (telefonos ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Length == 8 && t.All(char.IsDigit) ? $"{t[..4]}-{t[4..]}" : t));

    // ---------- Cédula y RUC ----------

    /// <summary>Cédula con guiones (001-230104-1025R). Vacío → null.</summary>
    public static string? Cedula(string? valor)
    {
        var texto = Compactar(valor);
        if (texto.Length == 0) return null;

        var partes = PatronCedula().Match(texto);
        if (!partes.Success)
            throw new ReglaNegocioException($"La cédula no tiene el formato correcto ({FormatoCedula}).");

        var municipio = partes.Groups[1].Value;
        var fecha = partes.Groups[2].Value;
        var consecutivo = partes.Groups[3].Value;
        var letra = partes.Groups[4].Value[0];
        ValidarCedula(municipio + fecha + consecutivo, letra, "la cédula");
        return $"{municipio}-{fecha}-{consecutivo}{letra}";
    }

    /// <summary>RUC de 14 caracteres sin guiones (ej. J0310000007669 o 0012301041025R). Vacío → null.</summary>
    public static string? Ruc(string? valor)
    {
        var texto = Compactar(valor).Replace("-", "", StringComparison.Ordinal);
        if (texto.Length == 0) return null;
        if (texto.Length != 14)
            throw new ReglaNegocioException($"El RUC debe tener 14 caracteres (ej. {EjemploRuc}).");

        if (PatronRucConLetra().IsMatch(texto)) return texto;

        if (PatronRucNatural().IsMatch(texto))
        {
            if (texto.StartsWith("0000", StringComparison.Ordinal))
                throw new ReglaNegocioException("Los RUC que inician con cuatro ceros no son válidos (aviso DGI 072/05-2022).");
            ValidarCedula(texto[..13], texto[13], "el RUC");
            return texto;
        }

        throw new ReglaNegocioException(
            $"El RUC no es válido. Debe ser 13 dígitos y una letra (persona natural) o J, N, R o E seguida de 13 dígitos (ej. {EjemploRuc}).");
    }

    /// <summary>
    /// Campo "RUC / Cédula" de clientes y facturas: con guiones se interpreta como cédula,
    /// sin guiones como RUC de 14 caracteres. Vacío → null.
    /// </summary>
    public static string? Documento(string? valor)
    {
        var texto = Compactar(valor);
        if (texto.Length == 0) return null;
        if (texto.Contains('-', StringComparison.Ordinal) && PatronCedula().IsMatch(texto)) return Cedula(texto);
        if (texto.Length == 14 && !texto.Contains('-', StringComparison.Ordinal)) return Ruc(texto);
        throw new ReglaNegocioException(
            $"RUC o cédula no válido. Cédula: {FormatoCedula} · RUC: 14 caracteres (ej. {EjemploRuc}).");
    }

    /// <summary>Letra de control: residuo de los 13 dígitos entre 23.</summary>
    internal static char LetraDeControl(string treceDigitos) =>
        LetrasControl[(int)(long.Parse(treceDigitos, CultureInfo.InvariantCulture) % 23)];

    private static void ValidarCedula(string treceDigitos, char letra, string nombre)
    {
        var fecha = treceDigitos.Substring(3, 6);
        if (!FechaValida(fecha))
            throw new ReglaNegocioException(
                $"Revise {nombre}: la fecha de nacimiento {fecha[..2]}/{fecha[2..4]}/{fecha[4..]} no existe.");
        if (letra != LetraDeControl(treceDigitos))
            throw new ReglaNegocioException($"Revise {nombre}: la letra final no corresponde a los números.");
    }

    /// <summary>ddmmaa válido en 19aa o en 20aa.</summary>
    private static bool FechaValida(string ddmmaa)
    {
        var dia = int.Parse(ddmmaa[..2], CultureInfo.InvariantCulture);
        var mes = int.Parse(ddmmaa[2..4], CultureInfo.InvariantCulture);
        var anio = int.Parse(ddmmaa[4..], CultureInfo.InvariantCulture);
        if (mes is < 1 or > 12 || dia < 1) return false;
        return dia <= DateTime.DaysInMonth(1900 + anio, mes) || dia <= DateTime.DaysInMonth(2000 + anio, mes);
    }

    private static string Compactar(string? valor) => Texto.Mayusculas(valor).Replace(" ", "", StringComparison.Ordinal);
}
