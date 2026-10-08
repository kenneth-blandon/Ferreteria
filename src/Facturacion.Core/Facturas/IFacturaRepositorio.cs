namespace Facturacion.Core.Facturas;

public interface IFacturaRepositorio
{
    /// <summary>
    /// Guarda la factura en una sola transacción: toma el siguiente número, inserta
    /// encabezado y líneas, descuenta existencias y registra los movimientos de inventario.
    /// Completa Id, Serie y Numero de la factura recibida.
    /// </summary>
    Factura Emitir(Factura factura, ReglasNumeracion numeracion, bool permitirStockNegativo);

    Factura? Obtener(long id);

    /// <summary>Encabezados (sin líneas), del más reciente al más antiguo.</summary>
    IReadOnlyList<Factura> Listar(FiltroFacturas filtro);

    /// <summary>Marca la factura como anulada y devuelve las existencias al inventario.</summary>
    Factura Anular(long id, string motivo, DateTime fecha);

    /// <summary>
    /// Reemplaza el contenido de una factura emitida (mismo número) en una sola transacción:
    /// ajusta existencias por la diferencia de cantidades y guarda la constancia con la versión anterior.
    /// </summary>
    Factura Modificar(Factura anterior, Factura nueva, ModificacionFactura modificacion, bool permitirStockNegativo);

    long LeerSiguienteNumero();

    bool ExisteNumero(string serie, long numero);
}
