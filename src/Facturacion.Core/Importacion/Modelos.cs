using Facturacion.Core.Catalogo;

namespace Facturacion.Core.Importacion;

/// <summary>Fila leída de un archivo: encabezado normalizado → texto de la celda.</summary>
public sealed record FilaArchivo(int Numero, IReadOnlyDictionary<string, string> Celdas);

/// <summary>Lee filas de un tipo de archivo (Excel, CSV...).</summary>
public interface ILectorArchivo
{
    IReadOnlyCollection<string> Extensiones { get; }
    IReadOnlyList<FilaArchivo> Leer(Stream contenido);
}

/// <summary>Genera archivos de inventario para revisión o conteo físico.</summary>
public interface IExportadorInventario
{
    byte[] Exportar(IReadOnlyList<Producto> productos, DateTime fecha);
    byte[] Plantilla();
}

/// <summary>Producto ya validado y normalizado, listo para el UPSERT.</summary>
public sealed class FilaProducto
{
    public int Fila { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    /// <summary>Null = la columna no venía o estaba vacía: se conserva la existencia actual.</summary>
    public decimal? Stock { get; set; }
    public decimal? StockMinimo { get; set; }
    public string? Categoria { get; set; }
    public string? Unidad { get; set; }
    public string? CodigoBarras { get; set; }
}

public sealed record ErrorImportacion(int Fila, string Mensaje);

/// <summary>Resultado de la escritura en base de datos.</summary>
public sealed record ResultadoCarga(int Insertados, int Actualizados, IReadOnlyList<ErrorImportacion> Errores);

public sealed class ResultadoImportacion
{
    public int FilasLeidas { get; set; }
    public int Insertados { get; set; }
    public int Actualizados { get; set; }
    public List<ErrorImportacion> Errores { get; set; } = [];
}
