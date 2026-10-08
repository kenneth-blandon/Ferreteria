using Facturacion.Core.Comun;

namespace Facturacion.Tests;

public class IdentificacionTests
{
    [Theory]
    [InlineData("89704920", "89704920")]
    [InlineData("8970-4920", "89704920")]
    [InlineData(" +505 8970 4920 ", "89704920")]
    [InlineData("(505) 2270-7704", "22707704")]
    public void Telefono_valido_se_normaliza_a_8_digitos(string entrada, string esperado) =>
        Assert.Equal(esperado, Identificacion.Telefono(entrada));

    [Theory]
    [InlineData("8970492")]
    [InlineData("897049201")]
    [InlineData("8970-49A0")]
    [InlineData("telefono")]
    public void Telefono_invalido(string entrada) =>
        Assert.Throws<ReglaNegocioException>(() => Identificacion.Telefono(entrada));

    [Fact]
    public void Telefono_vacio_es_opcional() => Assert.Null(Identificacion.Telefono("  "));

    [Fact]
    public void Varios_telefonos_del_negocio()
    {
        Assert.Equal("86326107 / 84588766", Identificacion.Telefonos("8632-6107 / 8458-8766"));
        Assert.Equal("8632-6107 / 8458-8766", Identificacion.FormatoTelefono("86326107 / 84588766"));
        Assert.Throws<ReglaNegocioException>(() => Identificacion.Telefonos("8632-6107 / 845"));
    }

    [Theory]
    [InlineData("001-010190-0001N", "001-010190-0001N")]
    [InlineData("0010101900001n", "001-010190-0001N")]
    [InlineData(" 001-010190-0001 N ", "001-010190-0001N")]
    public void Cedula_valida_con_o_sin_guiones(string entrada, string esperado) =>
        Assert.Equal(esperado, Identificacion.Cedula(entrada));

    [Fact]
    public void Cedula_con_letra_que_no_corresponde()
    {
        var error = Assert.Throws<ReglaNegocioException>(() => Identificacion.Cedula("001-010190-0001A"));
        Assert.Contains("letra", error.Message);
    }

    [Fact]
    public void Cedula_con_fecha_inexistente()
    {
        var error = Assert.Throws<ReglaNegocioException>(() => Identificacion.Cedula("001-310290-0001A"));
        Assert.Contains("fecha", error.Message);
    }

    [Fact]
    public void Cedula_con_formato_incorrecto() =>
        Assert.Throws<ReglaNegocioException>(() => Identificacion.Cedula("001-0101-0001N"));

    [Fact]
    public void Letra_de_control_es_el_residuo_entre_23() =>
        Assert.Equal('N', Identificacion.LetraDeControl("0010101900001"));

    [Theory]
    [InlineData("J0310000007669")]
    [InlineData("N0000000000019")]
    [InlineData("R1230003007661")]
    [InlineData("E1230003007661")]
    [InlineData("2411011840002F")]
    public void Ruc_valido_de_14_caracteres(string ruc) => Assert.Equal(ruc, Identificacion.Ruc(ruc.ToLowerInvariant()));

    [Fact]
    public void Ruc_con_cuatro_ceros_iniciales_no_es_valido()
    {
        var error = Assert.Throws<ReglaNegocioException>(() => Identificacion.Ruc("0000300766011M"));
        Assert.Contains("cuatro ceros", error.Message);
    }

    [Theory]
    [InlineData("J031000000766")]
    [InlineData("X0310000007669")]
    [InlineData("J03100000076690")]
    public void Ruc_con_formato_incorrecto(string ruc) =>
        Assert.Throws<ReglaNegocioException>(() => Identificacion.Ruc(ruc));

    [Fact]
    public void Documento_acepta_cedula_o_ruc()
    {
        Assert.Equal("001-010190-0001N", Identificacion.Documento("001-010190-0001n"));
        Assert.Equal("J0310000007669", Identificacion.Documento("j0310000007669"));
        Assert.Equal("2411011840002F", Identificacion.Documento("2411011840002F"));
        Assert.Null(Identificacion.Documento(""));
        Assert.Throws<ReglaNegocioException>(() => Identificacion.Documento("12345"));
    }
}
