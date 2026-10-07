using Facturacion.Core.Comun;

namespace Facturacion.Core.Facturas;

/// <summary>Cálculos de la factura. Es la única fuente de verdad de los totales.</summary>
public static class CalculadoraFactura
{
    public static decimal TotalLinea(decimal cantidad, decimal precioUnitario) =>
        Dinero.Redondear(cantidad * precioUnitario);

    /// <summary>
    /// Suma las líneas y aplica el IVA según la configuración:
    /// <list type="bullet">
    /// <item>Sin IVA (Cuota Fija): total = suma de líneas.</item>
    /// <item>Precios sin IVA: subtotal = suma, IVA = subtotal × %, total = subtotal + IVA.</item>
    /// <item>Precios con IVA: total = suma, subtotal = total ÷ (1 + %), IVA = diferencia.</item>
    /// </list>
    /// </summary>
    public static TotalesFactura Calcular(IEnumerable<decimal> totalesLinea, ConfiguracionIva iva)
    {
        var suma = Dinero.Redondear(totalesLinea.Sum());
        if (!iva.Habilitado || iva.Porcentaje <= 0)
            return new TotalesFactura(suma, 0, suma);

        var tasa = iva.Porcentaje / 100m;
        if (iva.PreciosIncluyenIva)
        {
            var subtotal = Dinero.Redondear(suma / (1 + tasa));
            return new TotalesFactura(subtotal, suma - subtotal, suma);
        }

        var impuesto = Dinero.Redondear(suma * tasa);
        return new TotalesFactura(suma, impuesto, suma + impuesto);
    }

    /// <summary>Vuelto a entregar. Lanza error si el efectivo recibido no alcanza.</summary>
    public static decimal Vuelto(decimal total, decimal recibido)
    {
        if (recibido < total)
            throw new ReglaNegocioException(
                $"El efectivo recibido ({Dinero.Formatear(recibido)}) es menor al total ({Dinero.Formatear(total)}).");
        return Dinero.Redondear(recibido - total);
    }
}
