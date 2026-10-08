using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using Facturacion.Core.Inventario;

namespace Facturacion.Tests;

public class FacturacionTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    [Fact]
    public void Emite_con_numero_consecutivo_y_descuenta_existencias()
    {
        var cable = e.CrearProducto("ELE-001", "Cable THHN #12", 28.50m, stock: 100);
        var tubo = e.CrearProducto("FON-001", "Tubo PVC 1/2", 95m, stock: 10);

        var factura = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(cable, 2.5m), Entorno.Linea(tubo, 3)));

        Assert.Equal("A-40658", factura.NumeroCompleto);
        Assert.Equal(71.25m + 285m, factura.Total);
        Assert.Equal(Factura.ConsumidorFinal, factura.ClienteNombre);
        Assert.Equal(97.5m, e.Catalogo.Obtener(cable.Id).Stock);
        Assert.Equal(7m, e.Catalogo.Obtener(tubo.Id).Stock);
        Assert.Equal(40659, e.Facturacion.ProximoNumero().Numero);

        var segunda = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(tubo, 1)));
        Assert.Equal(40659, segunda.Numero);
    }

    [Fact]
    public void Guarda_y_recupera_la_factura_completa()
    {
        var cable = e.CrearProducto("ELE-001", "Cable THHN #12", 28.50m);
        var emitida = e.Facturacion.Emitir(new SolicitudFactura(
            new ClienteSolicitud(null, "  juan pérez ", "001-010190-0001n", "barrio x", Guardar: true),
            [Entorno.Linea(cable, 2), new LineaSolicitud(null, "corte de tubo", "", 1, 15m)],
            MetodoPago.Efectivo, 100m, null));

        var leida = e.Facturacion.Obtener(emitida.Id);
        Assert.Equal("JUAN PÉREZ", leida.ClienteNombre);
        Assert.Equal("001-010190-0001N", leida.ClienteRuc);
        Assert.NotNull(leida.ClienteId);
        Assert.Equal(e.Reloj.Ahora, leida.Fecha);
        Assert.Equal(MetodoPago.Efectivo, leida.MetodoPago);
        Assert.Equal(EstadoFactura.Emitida, leida.Estado);
        Assert.Equal(72m, leida.Total);
        Assert.Equal(100m, leida.MontoRecibido);
        Assert.Equal(28m, leida.Vuelto);
        Assert.Collection(leida.Lineas,
            l => { Assert.Equal("ELE-001", l.Codigo); Assert.Equal(57m, l.Total); Assert.Equal(cable.Id, l.ProductoId); },
            l => { Assert.Equal("CORTE DE TUBO", l.Descripcion); Assert.Null(l.ProductoId); Assert.Equal("UND", l.Unidad); });
        Assert.Single(e.Clientes.Buscar("perez"));
    }

    [Fact]
    public void Usa_el_precio_editado_en_pantalla()
    {
        var martillo = e.CrearProducto("HER-001", "Martillo", 320m);
        var factura = e.Facturacion.Emitir(Entorno.Venta(new LineaSolicitud(martillo.Id, null, null, 1, 300m)));
        Assert.Equal(300m, factura.Total);
    }

    [Fact]
    public void Aplica_iva_cuando_esta_habilitado()
    {
        var f = e.Configuracion.ObtenerFacturacion();
        f.IvaHabilitado = true;
        e.Configuracion.GuardarFacturacion(f);
        var p = e.CrearProducto("X-1", "Producto", 100m);

        var factura = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        Assert.Equal((100m, 15m, 115m), (factura.Subtotal, factura.Iva, factura.Total));
    }

    [Fact]
    public void Rechaza_efectivo_insuficiente_sin_consumir_numero()
    {
        var p = e.CrearProducto("X-1", "Producto", 100m);
        Assert.Throws<ReglaNegocioException>(() =>
            e.Facturacion.Emitir(new SolicitudFactura(null, [Entorno.Linea(p, 1)], MetodoPago.Efectivo, 50m, null)));
        Assert.Equal(40658, e.Facturacion.ProximoNumero().Numero);
        Assert.Equal(10m, e.Catalogo.Obtener(p.Id).Stock);
    }

    [Fact]
    public void Bloquea_cuando_se_agota_el_rango_autorizado()
    {
        var f = e.Configuracion.ObtenerFacturacion();
        f.RangoDesde = 1;
        f.RangoHasta = 2;
        f.SiguienteNumero = 2;
        e.Configuracion.GuardarFacturacion(f);
        var p = e.CrearProducto("X-1", "Producto", 10m);

        Assert.Equal(2, e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1))).Numero);
        var error = Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1))));
        Assert.Contains("agotó", error.Message);
    }

    [Fact]
    public void No_permite_configurar_un_numero_ya_usado()
    {
        var p = e.CrearProducto("X-1", "Producto", 10m);
        e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        var f = e.Configuracion.ObtenerFacturacion();
        f.SiguienteNumero = 40658;
        Assert.Throws<ReglaNegocioException>(() => e.Configuracion.GuardarFacturacion(f));
    }

    [Fact]
    public void Respeta_la_opcion_de_no_vender_sin_existencias()
    {
        var f = e.Configuracion.ObtenerFacturacion();
        f.PermitirStockNegativo = false;
        e.Configuracion.GuardarFacturacion(f);
        var p = e.CrearProducto("X-1", "Producto", 10m, stock: 2);

        var error = Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 3))));
        Assert.Contains("Existencia insuficiente", error.Message);
        Assert.Equal(40658, e.Facturacion.ProximoNumero().Numero);
    }

    [Fact]
    public void Anular_devuelve_existencias_y_exige_motivo()
    {
        var p = e.CrearProducto("X-1", "Producto", 10m, stock: 5);
        var factura = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 2)));
        Assert.Equal(3m, e.Catalogo.Obtener(p.Id).Stock);

        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Anular(factura.Id, ""));
        var anulada = e.Facturacion.Anular(factura.Id, "Error de digitación");

        Assert.Equal(EstadoFactura.Anulada, anulada.Estado);
        Assert.Equal("Error de digitación", anulada.MotivoAnulacion);
        Assert.Equal(5m, e.Catalogo.Obtener(p.Id).Stock);
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Anular(factura.Id, "otra vez"));

        var movimientos = e.Inventario.Movimientos(p.Id);
        Assert.Equal([TipoMovimiento.Anulacion, TipoMovimiento.Venta, TipoMovimiento.Inicial], movimientos.Select(m => m.Tipo));
    }

    [Fact]
    public void Lista_por_fecha_y_busca_por_numero_en_cualquier_fecha()
    {
        var p = e.CrearProducto("X-1", "Producto", 10m);
        var ayer = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        e.Reloj.Ahora = e.Reloj.Ahora.AddDays(1);
        e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        var hoy = DateOnly.FromDateTime(e.Reloj.Ahora);

        Assert.Single(e.Facturacion.Listar(new FiltroFacturas(hoy, hoy)));
        var porNumero = e.Facturacion.Listar(new FiltroFacturas(hoy, hoy, $"A-{ayer.Numero}"));
        Assert.Equal(ayer.Id, Assert.Single(porNumero).Id);
    }

    [Fact]
    public void Valida_lineas()
    {
        var p = e.CrearProducto("X-1", "Producto", 10m);
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta()));
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 0))));
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta(new LineaSolicitud(null, " ", null, 1, 5))));
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta(new LineaSolicitud(999, null, null, 1, 5))));
    }

    [Fact]
    public void Pago_con_tarjeta_guarda_referencia()
    {
        var p = e.CrearProducto("X-1", "Producto", 10m);
        var factura = e.Facturacion.Emitir(new SolicitudFactura(null, [Entorno.Linea(p, 1)], MetodoPago.Tarjeta, null, "pos-123"));
        var leida = e.Facturacion.Obtener(factura.Id);
        Assert.Equal(MetodoPago.Tarjeta, leida.MetodoPago);
        Assert.Equal("POS-123", leida.Referencia);
        Assert.Null(leida.MontoRecibido);
    }

    [Fact]
    public void Cierre_de_caja_suma_por_metodo_y_excluye_anuladas()
    {
        var p = e.CrearProducto("X-1", "Producto", 100m, stock: 50);
        e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 2)));
        e.Facturacion.Emitir(new SolicitudFactura(null, [Entorno.Linea(p, 3)], MetodoPago.Transferencia, null, null));
        var anulada = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 4)));
        e.Facturacion.Anular(anulada.Id, "Error");

        var cierre = e.Reportes.Cierre(DateOnly.FromDateTime(e.Reloj.Ahora));

        Assert.Equal(3, cierre.FacturasEmitidas);
        Assert.Equal(1, cierre.FacturasAnuladas);
        Assert.Equal(600m, cierre.TotalVendido);
        Assert.Equal(400m, cierre.TotalAnulado);
        Assert.Equal((40658L, 40661L), (cierre.PrimerNumero!.Value, cierre.UltimoNumero!.Value));
        Assert.Equal(300m, cierre.PorMetodo.Single(m => m.Metodo == MetodoPago.Efectivo).Total);
        Assert.Equal(300m, cierre.PorMetodo.Single(m => m.Metodo == MetodoPago.Transferencia).Total);
        Assert.Equal(0m, cierre.PorMetodo.Single(m => m.Metodo == MetodoPago.Tarjeta).Total);
        Assert.Equal(6m, Assert.Single(cierre.ProductosMasVendidos).Cantidad);
        Assert.Equal(4, cierre.Facturas.Count);
    }

    [Fact]
    public void Configuracion_por_defecto_sin_iva_y_con_talonario()
    {
        var ajustes = e.Configuracion.Obtener();
        Assert.False(ajustes.Facturacion.IvaHabilitado);
        Assert.Equal("A", ajustes.Facturacion.Serie);
        Assert.Equal(44000, ajustes.Facturacion.RangoHasta);
        Assert.Equal(22, ajustes.Impresion.Plantilla.Filas.Maximo);
        Assert.False(string.IsNullOrWhiteSpace(ajustes.Negocio.NombreComercial));
    }

    [Fact]
    public void No_permite_mas_lineas_que_el_talonario()
    {
        var lineas = Enumerable.Range(1, 23)
            .Select(i => new LineaSolicitud(null, $"Artículo {i}", null, 1, 10m))
            .ToArray();
        var error = Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(Entorno.Venta(lineas)));
        Assert.Contains("22 líneas", error.Message);
        Assert.Equal(22, e.Facturacion.Emitir(Entorno.Venta(lineas[..22])).Lineas.Count);
    }

    [Fact]
    public void Valida_el_ruc_o_cedula_del_cliente()
    {
        var p = e.CrearProducto("X-1", "Producto", 10m);
        Assert.Throws<ReglaNegocioException>(() => e.Facturacion.Emitir(new SolicitudFactura(
            new ClienteSolicitud(null, "Juan", "12345", null), [Entorno.Linea(p, 1)], MetodoPago.Efectivo, null, null)));

        var factura = e.Facturacion.Emitir(new SolicitudFactura(
            new ClienteSolicitud(null, "Ferretería S.A.", " j0310000007669 ", null), [Entorno.Linea(p, 1)], MetodoPago.Efectivo, null, null));
        Assert.Equal("J0310000007669", factura.ClienteRuc);
    }
}
