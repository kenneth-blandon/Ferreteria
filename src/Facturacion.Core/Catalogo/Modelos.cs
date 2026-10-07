namespace Facturacion.Core.Catalogo;

public sealed class Categoria
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public int TotalProductos { get; set; }
}

public sealed class Producto
{
    public long Id { get; set; }
    public string Codigo { get; set; } = "";
    public string? CodigoBarras { get; set; }
    public string Nombre { get; set; } = "";
    public long? CategoriaId { get; set; }
    public string? Categoria { get; set; }
    public string Unidad { get; set; } = "UND";
    public decimal Precio { get; set; }
    public decimal Stock { get; set; }
    /// <summary>Si es mayor que cero, el producto genera alerta al llegar a este nivel.</summary>
    public decimal StockMinimo { get; set; }
    public DateTime Actualizado { get; set; }

    public bool BajoStock => StockMinimo > 0 && Stock <= StockMinimo;
}

/// <summary>Datos que llegan del formulario de producto.</summary>
public sealed record DatosProducto(
    string? Codigo,
    string? CodigoBarras,
    string? Nombre,
    long? CategoriaId,
    string? Unidad,
    decimal Precio,
    decimal StockMinimo,
    decimal StockInicial = 0);

public sealed record FiltroProductos(
    string? Texto = null,
    long? CategoriaId = null,
    bool SoloBajoStock = false,
    int Pagina = 1,
    int TamanoPagina = 50);
