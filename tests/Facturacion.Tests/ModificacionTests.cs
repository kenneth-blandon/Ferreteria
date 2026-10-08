using Facturacion.Core.Comun;
using Facturacion.Core.Facturas;
using Facturacion.Core.Inventario;

namespace Facturacion.Tests;

public class ModificacionTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    private static SolicitudModificacion Correccion(SolicitudFactura datos, string? motivo = "Cliente pidió más cable", string? empleado = "Carlos") =>
        new(datos.Cliente, datos.Lineas, datos.MetodoPago, datos.MontoRecibido, datos.Referencia, motivo, empleado);

    [Fact]
    public void Modifica_conservando_numero_y_fecha_y_ajusta_existencias()
    {
        var cable = e.CrearProducto("ELE-001", "Cable", 28.50m, stock: 10);
        var tubo = e.CrearProducto("FON-001", "Tubo", 95m, stock: 10);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(cable, 2), Entorno.Linea(tubo, 3)));
        Assert.Equal((8m, 7m), (e.Catalogo.Obtener(cable.Id).Stock, e.Catalogo.Obtener(tubo.Id).Stock));

        e.Reloj.Ahora = e.Reloj.Ahora.AddHours(2);
        var nueva = Entorno.Venta(Entorno.Linea(cable, 5), new LineaSolicitud(null, "corte", "UND", 1, 15m));
        var modificada = e.Facturacion.Modificar(original.Id, Correccion(nueva));

        Assert.Equal(original.Numero, modificada.Numero);
        Assert.Equal(original.Fecha, modificada.Fecha);
        Assert.Equal(e.Reloj.Ahora, modificada.FechaModificacion);
        Assert.True(modificada.Modificada);
        Assert.Equal(5 * 28.50m + 15m, modificada.Total);
        Assert.Equal(2, modificada.Lineas.Count);
        Assert.Equal((5m, 10m), (e.Catalogo.Obtener(cable.Id).Stock, e.Catalogo.Obtener(tubo.Id).Stock));

        var constancia = Assert.Single(modificada.Modificaciones);
        Assert.Equal("CARLOS", constancia.Empleado);
        Assert.Equal("Cliente pidió más cable", constancia.Motivo);
        Assert.Equal(original.Total, constancia.TotalAnterior);
        Assert.Contains(constancia.Cambios, c => c.Contains("cantidad 2 → 5"));
        Assert.Contains(constancia.Cambios, c => c.StartsWith("Se quitó TUBO"));
        Assert.Contains(constancia.Cambios, c => c.StartsWith("Se agregó CORTE"));
        Assert.Contains(constancia.Cambios, c => c.StartsWith("Total:"));

        Assert.Contains(e.Inventario.Movimientos(cable.Id), m => m.Tipo == TipoMovimiento.Modificacion && m.Cantidad == -3);
        Assert.Contains(e.Inventario.Movimientos(tubo.Id), m => m.Tipo == TipoMovimiento.Modificacion && m.Cantidad == 3);
        Assert.Equal(40659, e.Facturacion.ProximoNumero().Numero);

        var hoy = DateOnly.FromDateTime(e.Reloj.Ahora);
        Assert.True(Assert.Single(e.Facturacion.Listar(new FiltroFacturas(hoy, hoy))).Modificada);
        Assert.Equal(modificada.Total, e.Reportes.Cierre(hoy).TotalVendido);
    }

    [Fact]
    public void Sin_cambios_no_se_modifica()
    {
        var p = e.CrearProducto("A", "Alfa", 10m);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 2)));

        Assert.Empty(e.Facturacion.CompararCambios(original.Id, Entorno.Venta(Entorno.Linea(p, 2))));
        var error = Assert.Throws<ReglaNegocioException>(() =>
            e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 2)))));
        Assert.Contains("No se detectaron cambios", error.Message);
    }

    [Fact]
    public void Comparar_cambios_no_guarda_nada()
    {
        var p = e.CrearProducto("A", "Alfa", 10m, stock: 10);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 2)));

        var cambios = e.Facturacion.CompararCambios(original.Id, new SolicitudFactura(
            new ClienteSolicitud(null, "Juan", null, null, Guardar: true), [Entorno.Linea(p, 4)], MetodoPago.Tarjeta, null, null));

        Assert.Contains("Cliente: CONSUMIDOR FINAL → JUAN", cambios);
        Assert.Contains("Forma de pago: Efectivo → Tarjeta", cambios);
        Assert.Equal(8m, e.Catalogo.Obtener(p.Id).Stock);
        Assert.Empty(e.Facturacion.Obtener(original.Id).Modificaciones);
        Assert.Empty(e.Clientes.Buscar("juan"));
    }

    [Theory]
    [InlineData(null, "Carlos")]
    [InlineData("abc", "Carlos")]
    [InlineData("Error de digitación", null)]
    [InlineData("Error de digitación", "  ")]
    public void Motivo_y_empleado_son_obligatorios(string? motivo, string? empleado)
    {
        var p = e.CrearProducto("A", "Alfa", 10m);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 2)));
        Assert.Throws<ReglaNegocioException>(() =>
            e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 3)), motivo, empleado)));
        Assert.Equal(2m, e.Facturacion.Obtener(original.Id).Lineas[0].Cantidad);
    }

    [Fact]
    public void Una_factura_anulada_no_se_modifica()
    {
        var p = e.CrearProducto("A", "Alfa", 10m);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 2)));
        e.Facturacion.Anular(original.Id, "Error");
        Assert.Throws<ReglaNegocioException>(() =>
            e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 3)))));
    }

    [Fact]
    public void Respeta_existencias_si_no_se_permite_stock_negativo()
    {
        var f = e.Configuracion.ObtenerFacturacion();
        f.PermitirStockNegativo = false;
        e.Configuracion.GuardarFacturacion(f);
        var p = e.CrearProducto("A", "Alfa", 10m, stock: 5);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 4)));

        // Quedan 1 en bodega + 4 de la factura = 5 disponibles para la corrección.
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 6)))));
        Assert.Equal(1m, e.Catalogo.Obtener(p.Id).Stock);
        e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 5))));
        Assert.Equal(0m, e.Catalogo.Obtener(p.Id).Stock);
    }

    [Fact]
    public void Varias_modificaciones_quedan_en_el_historial()
    {
        var p = e.CrearProducto("A", "Alfa", 10m);
        var original = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 2)), "Primera corrección", "Ana"));
        e.Reloj.Ahora = e.Reloj.Ahora.AddMinutes(5);
        var final = e.Facturacion.Modificar(original.Id, Correccion(Entorno.Venta(Entorno.Linea(p, 3)), "Segunda corrección", "Luis"));

        Assert.Equal(["LUIS", "ANA"], final.Modificaciones.Select(m => m.Empleado));
        Assert.Equal(30m, final.Total);
    }
}
