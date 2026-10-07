using Facturacion.Core.Catalogo;
using Facturacion.Core.Comun;

namespace Facturacion.Core.Inventario;

public enum TipoMovimiento { Inicial, Entrada, Salida, Conteo, Venta, Anulacion, Importacion }

/// <summary>Historial (kardex simple) de cada cambio de existencias.</summary>
public sealed class MovimientoInventario
{
    public long Id { get; set; }
    public long ProductoId { get; set; }
    public DateTime Fecha { get; set; }
    public TipoMovimiento Tipo { get; set; }
    /// <summary>Cambio aplicado: positivo entra, negativo sale.</summary>
    public decimal Cantidad { get; set; }
    public decimal StockResultante { get; set; }
    public string? Referencia { get; set; }
    public string? Nota { get; set; }
}

public enum TipoAjuste
{
    /// <summary>Llegó mercadería: suma.</summary>
    Entrada,
    /// <summary>Pérdida, daño o consumo: resta.</summary>
    Salida,
    /// <summary>Conteo físico: fija la existencia al valor contado.</summary>
    Conteo
}

public sealed record SolicitudAjuste(TipoAjuste Tipo, decimal Cantidad, string? Nota);

public interface IInventarioRepositorio
{
    /// <summary>Lee la existencia actual, calcula la nueva y registra el movimiento (todo en una transacción).</summary>
    MovimientoInventario Ajustar(long productoId, TipoMovimiento tipo, Func<decimal, decimal> calcularNuevoStock, string? nota, DateTime fecha);

    IReadOnlyList<MovimientoInventario> Movimientos(long productoId, int limite);
}

/// <summary>Ajustes rápidos de existencias y alertas de stock mínimo.</summary>
public sealed class ServicioInventario(IInventarioRepositorio inventario, IProductoRepositorio productos, TimeProvider reloj)
{
    public MovimientoInventario Ajustar(long productoId, SolicitudAjuste solicitud)
    {
        _ = productos.Obtener(productoId) ?? throw new NoEncontradoException("El producto no existe.");

        var cantidad = Dinero.RedondearCantidad(solicitud.Cantidad);
        if (cantidad < 0 || cantidad > Dinero.CantidadMaxima)
            throw new ReglaNegocioException("La cantidad no es válida.");
        if (cantidad == 0 && solicitud.Tipo != TipoAjuste.Conteo)
            throw new ReglaNegocioException("La cantidad debe ser mayor que cero.");

        var nota = Texto.LimpiarONulo(solicitud.Nota);
        if (nota is { Length: > 200 }) throw new ReglaNegocioException("La nota admite máximo 200 caracteres.");

        var (tipo, calcular) = solicitud.Tipo switch
        {
            TipoAjuste.Entrada => (TipoMovimiento.Entrada, (Func<decimal, decimal>)(actual => actual + cantidad)),
            TipoAjuste.Salida => (TipoMovimiento.Salida, actual => actual - cantidad),
            TipoAjuste.Conteo => (TipoMovimiento.Conteo, _ => cantidad),
            _ => throw new ReglaNegocioException("Tipo de ajuste no válido.")
        };

        return inventario.Ajustar(productoId, tipo, calcular, nota, reloj.AhoraLocal());
    }

    public IReadOnlyList<MovimientoInventario> Movimientos(long productoId, int limite = 100)
    {
        _ = productos.Obtener(productoId) ?? throw new NoEncontradoException("El producto no existe.");
        return inventario.Movimientos(productoId, Math.Clamp(limite, 1, 500));
    }

    public IReadOnlyList<Producto> Alertas() => productos.BajoStock();
}
