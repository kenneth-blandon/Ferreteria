using System.Globalization;

namespace Facturacion.Datos;

/// <summary>Utilidades para armar consultas.</summary>
internal static class Sql
{
    /// <summary>Las fechas se guardan como texto ISO: se ordenan y comparan correctamente.</summary>
    public static string Fecha(DateTime fecha) => fecha.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string InicioDia(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00";

    /// <summary>Escapa comodines de LIKE (se usa con ESCAPE '\').</summary>
    public static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public static string Contiene(string texto) => "%" + EscaparLike(texto) + "%";

    public static string EmpiezaCon(string texto) => EscaparLike(texto) + "%";

    /// <summary>Código de error SQLite de restricciones (UNIQUE, CHECK, FOREIGN KEY).</summary>
    public const int ErrorRestriccion = 19;
}
