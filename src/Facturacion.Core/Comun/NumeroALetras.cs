namespace Facturacion.Core.Comun;

/// <summary>
/// Convierte montos a letras en español para el texto del pagaré:
/// 1,250.50 → "MIL DOSCIENTOS CINCUENTA CÓRDOBAS CON 50/100".
/// </summary>
public static class NumeroALetras
{
    private static readonly string[] HastaVeintinueve =
    [
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE",
        "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];

    private static readonly string[] Decenas =
        ["", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];

    private static readonly string[] Centenas =
        ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

    /// <summary>Número entero a letras: 21 → "VEINTIUNO", 1000 → "MIL".</summary>
    public static string Convertir(long numero)
    {
        if (numero < 0) return "MENOS " + Convertir(-numero);
        if (numero == 0) return "CERO";

        var partes = new List<string>();
        var millones = numero / 1_000_000;
        var miles = numero / 1_000 % 1_000;
        var unidades = numero % 1_000;

        if (millones > 0)
            partes.Add(millones == 1 ? "UN MILLÓN" : Apocopar(Convertir(millones)) + " MILLONES");
        if (miles > 0)
            partes.Add(miles == 1 ? "MIL" : Apocopar(MenorDeMil((int)miles)) + " MIL");
        if (unidades > 0)
            partes.Add(MenorDeMil((int)unidades));

        return string.Join(' ', partes);
    }

    /// <summary>Monto en córdobas a letras con centavos en formato XX/100.</summary>
    public static string Monto(decimal monto, string monedaSingular = "CÓRDOBA", string monedaPlural = "CÓRDOBAS")
    {
        monto = Dinero.Redondear(Math.Abs(monto));
        var entero = (long)Math.Truncate(monto);
        var centavos = (int)((monto - entero) * 100);

        var moneda = entero == 1 ? monedaSingular : monedaPlural;
        // "UN MILLÓN DE CÓRDOBAS", "DOS MILLONES DE CÓRDOBAS"
        if (entero >= 1_000_000 && entero % 1_000_000 == 0) moneda = "DE " + moneda;

        return $"{Apocopar(Convertir(entero))} {moneda} CON {centavos:00}/100";
    }

    private static string MenorDeMil(int numero)
    {
        if (numero == 100) return "CIEN";

        var partes = new List<string>();
        var centena = numero / 100;
        var resto = numero % 100;

        if (centena > 0) partes.Add(Centenas[centena]);
        if (resto > 0)
        {
            if (resto < 30)
            {
                partes.Add(HastaVeintinueve[resto]);
            }
            else
            {
                var decena = resto / 10;
                var unidad = resto % 10;
                partes.Add(unidad == 0 ? Decenas[decena] : $"{Decenas[decena]} Y {HastaVeintinueve[unidad]}");
            }
        }
        return string.Join(' ', partes);
    }

    /// <summary>"UNO" pierde la última letra delante de un sustantivo: "VEINTIÚN MIL", "UN CÓRDOBA".</summary>
    private static string Apocopar(string texto)
    {
        if (texto.EndsWith("VEINTIUNO", StringComparison.Ordinal)) return texto[..^"VEINTIUNO".Length] + "VEINTIÚN";
        if (texto.EndsWith("UNO", StringComparison.Ordinal)) return texto[..^3] + "UN";
        return texto;
    }
}
