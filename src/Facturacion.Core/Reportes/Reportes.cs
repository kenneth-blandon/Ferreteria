using Facturacion.Core.Facturas;

namespace Facturacion.Core.Reportes;

/// <summary>Resumen diario de ventas (cierre de caja).</summary>
public sealed class ResumenCierre
{
    public DateOnly Fecha { get; set; }
    public int FacturasEmitidas { get; set; }
    public int FacturasAnuladas { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal TotalVendido { get; set; }
    public decimal TotalAnulado { get; set; }
    /// <summary>Primer y último número usados en el día (para cuadrar con el talonario físico).</summary>
    public long? PrimerNumero { get; set; }
    public long? UltimoNumero { get; set; }
    public List<TotalPorMetodo> PorMetodo { get; set; } = [];
    public List<ProductoVendido> ProductosMasVendidos { get; set; } = [];
    public List<Factura> Facturas { get; set; } = [];
}

public sealed class TotalPorMetodo
{
    public MetodoPago Metodo { get; set; }
    public int Facturas { get; set; }
    public decimal Total { get; set; }
}

public sealed class ProductoVendido
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal Total { get; set; }
}

public interface IReporteRepositorio
{
    ResumenCierre Cierre(DateOnly fecha);
}

public sealed class ServicioReportes(IReporteRepositorio reportes, IFacturaRepositorio facturas)
{
    public ResumenCierre Cierre(DateOnly fecha)
    {
        var resumen = reportes.Cierre(fecha);

        // Siempre se muestran los tres métodos, aunque estén en cero.
        resumen.PorMetodo = Enum.GetValues<MetodoPago>()
            .Select(m => resumen.PorMetodo.FirstOrDefault(p => p.Metodo == m) ?? new TotalPorMetodo { Metodo = m })
            .ToList();

        resumen.Facturas = facturas.Listar(new FiltroFacturas(fecha, fecha))
            .OrderBy(f => f.Numero)
            .ToList();
        return resumen;
    }
}
