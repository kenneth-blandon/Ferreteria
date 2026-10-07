using Facturacion.Core.Comun;
using Facturacion.Core.Facturas;
using Facturacion.Core.Importacion;

namespace Facturacion.Tests;

public class NumeroALetrasTests
{
    [Theory]
    [InlineData(0, "CERO")]
    [InlineData(1, "UNO")]
    [InlineData(16, "DIECISÉIS")]
    [InlineData(21, "VEINTIUNO")]
    [InlineData(35, "TREINTA Y CINCO")]
    [InlineData(100, "CIEN")]
    [InlineData(101, "CIENTO UNO")]
    [InlineData(500, "QUINIENTOS")]
    [InlineData(999, "NOVECIENTOS NOVENTA Y NUEVE")]
    [InlineData(1000, "MIL")]
    [InlineData(1001, "MIL UNO")]
    [InlineData(21000, "VEINTIÚN MIL")]
    [InlineData(31500, "TREINTA Y UN MIL QUINIENTOS")]
    [InlineData(100000, "CIEN MIL")]
    [InlineData(1000000, "UN MILLÓN")]
    [InlineData(2500750, "DOS MILLONES QUINIENTOS MIL SETECIENTOS CINCUENTA")]
    [InlineData(21000000, "VEINTIÚN MILLONES")]
    public void Convierte_enteros(long numero, string esperado) =>
        Assert.Equal(esperado, NumeroALetras.Convertir(numero));

    [Theory]
    [InlineData("1", "UN CÓRDOBA CON 00/100")]
    [InlineData("21", "VEINTIÚN CÓRDOBAS CON 00/100")]
    [InlineData("1250.50", "MIL DOSCIENTOS CINCUENTA CÓRDOBAS CON 50/100")]
    [InlineData("0.75", "CERO CÓRDOBAS CON 75/100")]
    [InlineData("201", "DOSCIENTOS UN CÓRDOBAS CON 00/100")]
    [InlineData("1000000", "UN MILLÓN DE CÓRDOBAS CON 00/100")]
    [InlineData("3890.005", "TRES MIL OCHOCIENTOS NOVENTA CÓRDOBAS CON 01/100")]
    public void Convierte_montos(string monto, string esperado) =>
        Assert.Equal(esperado, NumeroALetras.Monto(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture)));
}

public class TextoTests
{
    [Fact]
    public void Normaliza_mayusculas_y_espacios() =>
        Assert.Equal("TUBO PVC 1/2", Texto.Mayusculas("  tubo   pvc 1/2 "));

    [Fact]
    public void Clave_de_busqueda_sin_tildes() =>
        Assert.Equal("TUBERIA NINO ELECTRICOS", Texto.ClaveBusqueda("Tubería", null, "niño", "  Eléctricos"));

    [Fact]
    public void Separa_terminos() =>
        Assert.Equal(["TUBO", "3/4"], Texto.Terminos(" tubo   3/4 "));
}

public class LectorNumerosTests
{
    [Theory]
    [InlineData("1250.5", 1250.5)]
    [InlineData("1,250.50", 1250.5)]
    [InlineData("1.250,50", 1250.5)]
    [InlineData("C$ 3,890.00", 3890)]
    [InlineData("12,5", 12.5)]
    [InlineData("0,250", 0.25)]
    [InlineData("1,250", 1250)]
    [InlineData("1,250,000", 1250000)]
    [InlineData("-3", -3)]
    public void Interpreta_formatos(string texto, double esperado)
    {
        Assert.True(LectorNumeros.Intentar(texto, out var valor));
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Rechaza_texto_invalido(string? texto) => Assert.False(LectorNumeros.Intentar(texto, out _));
}

public class CalculadoraFacturaTests
{
    [Fact]
    public void Sin_iva_el_total_es_la_suma()
    {
        var t = CalculadoraFactura.Calcular([100m, 50.25m], ConfiguracionIva.SinIva);
        Assert.Equal(new TotalesFactura(150.25m, 0, 150.25m), t);
    }

    [Fact]
    public void Iva_sumado_al_subtotal()
    {
        var t = CalculadoraFactura.Calcular([100m], new ConfiguracionIva(true, 15, PreciosIncluyenIva: false));
        Assert.Equal(new TotalesFactura(100m, 15m, 115m), t);
    }

    [Fact]
    public void Iva_incluido_en_el_precio()
    {
        var t = CalculadoraFactura.Calcular([115m], new ConfiguracionIva(true, 15, PreciosIncluyenIva: true));
        Assert.Equal(new TotalesFactura(100m, 15m, 115m), t);
    }

    [Fact]
    public void Total_de_linea_redondea_a_dos_decimales() =>
        Assert.Equal(83.33m, CalculadoraFactura.TotalLinea(2.5m, 33.33m));

    [Fact]
    public void Vuelto_y_efectivo_insuficiente()
    {
        Assert.Equal(20m, CalculadoraFactura.Vuelto(480m, 500m));
        Assert.Throws<ReglaNegocioException>(() => CalculadoraFactura.Vuelto(480m, 400m));
    }
}
