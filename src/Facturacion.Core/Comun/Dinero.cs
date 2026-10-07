using System.Globalization;

namespace Facturacion.Core.Comun;

/// <summary>Reglas únicas de redondeo y formato para montos y cantidades.</summary>
public static class Dinero
{
    public const decimal MontoMaximo = 99_999_999m;
    public const decimal CantidadMaxima = 100_000m;

    /// <summary>Redondea un monto a 2 decimales (0.005 sube a 0.01).</summary>
    public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>Las cantidades admiten hasta 3 decimales (metros, libras, galones...).</summary>
    public static decimal RedondearCantidad(decimal valor) => Math.Round(valor, 3, MidpointRounding.AwayFromZero);

    /// <summary>Formato "C$ 1,234.50".</summary>
    public static string Formatear(decimal valor) => "C$ " + Numero(valor);

    /// <summary>Formato "1,234.50" sin símbolo.</summary>
    public static string Numero(decimal valor) => Redondear(valor).ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Cantidad sin ceros sobrantes: 2 → "2", 2.5 → "2.5".</summary>
    public static string Cantidad(decimal valor) => RedondearCantidad(valor).ToString("#,##0.###", CultureInfo.InvariantCulture);
}
