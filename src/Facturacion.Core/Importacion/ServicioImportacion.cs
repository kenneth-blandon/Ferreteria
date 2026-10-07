using Facturacion.Core.Catalogo;
using Facturacion.Core.Comun;

namespace Facturacion.Core.Importacion;

/// <summary>
/// Importa el catálogo desde Excel o CSV:
/// 1) lee las filas, 2) normaliza (mayúsculas, espacios), 3) valida y detecta códigos repetidos
/// dentro del archivo, 4) delega a la base de datos un UPSERT en una sola transacción.
/// Si un código ya existe se actualiza; si no, se inserta.
/// </summary>
public sealed class ServicioImportacion(
    IProductoRepositorio productos,
    IEnumerable<ILectorArchivo> lectores,
    IExportadorInventario exportador,
    TimeProvider reloj)
{
    public const int MaximoFilas = 50_000;

    // Nombres de columna aceptados (ya normalizados: sin tildes, mayúsculas, sin espacios ni símbolos).
    private static readonly string[] ColCodigo = ["CODIGO", "COD", "CODIGOINTERNO", "SKU", "REFERENCIA"];
    private static readonly string[] ColNombre = ["NOMBRE", "DESCRIPCION", "PRODUCTO", "ARTICULO"];
    private static readonly string[] ColPrecio = ["PRECIO", "PRECIOUNITARIO", "PRECIOVENTA", "PUNIT", "PU"];
    private static readonly string[] ColStock = ["STOCK", "EXISTENCIA", "EXISTENCIAS", "INVENTARIO", "CANTIDAD"];
    private static readonly string[] ColConteo = ["CONTEOFISICO", "CONTEO"];
    private static readonly string[] ColStockMinimo = ["STOCKMINIMO", "MINIMO", "EXISTENCIAMINIMA", "STOCKMIN"];
    private static readonly string[] ColCategoria = ["CATEGORIA", "FAMILIA", "LINEA", "GRUPO"];
    private static readonly string[] ColUnidad = ["UNIDAD", "UM", "UNIDADMEDIDA", "MEDIDA"];
    private static readonly string[] ColBarras = ["CODIGOBARRAS", "CODIGODEBARRAS", "BARRAS", "EAN", "UPC"];

    /// <summary>Normaliza un encabezado: "Código de Barras" → "CODIGODEBARRAS".</summary>
    public static string NormalizarEncabezado(string? encabezado) =>
        new(Texto.ClaveBusqueda(encabezado).Where(char.IsLetterOrDigit).ToArray());

    public ResultadoImportacion Importar(Stream contenido, string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo ?? "").ToLowerInvariant();
        var lector = lectores.FirstOrDefault(l => l.Extensiones.Contains(extension))
            ?? throw new ReglaNegocioException("Formato no admitido. Use un archivo Excel (.xlsx) o CSV (.csv).");

        IReadOnlyList<FilaArchivo> filas;
        try
        {
            filas = lector.Leer(contenido);
        }
        catch (Exception ex) when (ex is not ReglaNegocioException)
        {
            throw new ReglaNegocioException(
                "No se pudo leer el archivo. Verifique que no esté dañado ni abierto en otro programa. Detalle: " + ex.Message);
        }

        if (filas.Count == 0)
            throw new ReglaNegocioException("El archivo no tiene filas de productos (la primera fila debe ser el encabezado).");
        if (filas.Count > MaximoFilas)
            throw new ReglaNegocioException($"El archivo tiene demasiadas filas (máximo {MaximoFilas:N0}).");

        var columnas = filas.SelectMany(f => f.Celdas.Keys).ToHashSet();
        var faltantes = new List<string>();
        if (!ColCodigo.Any(columnas.Contains)) faltantes.Add("código");
        if (!ColNombre.Any(columnas.Contains)) faltantes.Add("nombre");
        if (!ColPrecio.Any(columnas.Contains)) faltantes.Add("precio");
        if (faltantes.Count > 0)
            throw new ReglaNegocioException(
                $"Faltan columnas obligatorias: {string.Join(", ", faltantes)}. Descargue la plantilla para ver el formato.");

        var resultado = new ResultadoImportacion();
        var validas = new List<FilaProducto>();
        var codigosVistos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var barrasVistas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            if (fila.Celdas.Values.All(string.IsNullOrWhiteSpace)) continue;
            resultado.FilasLeidas++;

            var (producto, error) = Convertir(fila);
            if (error is null && codigosVistos.TryGetValue(producto!.Codigo, out var filaCodigo))
                error = $"El código '{producto.Codigo}' está repetido (también en la fila {filaCodigo}).";
            if (error is null && producto!.CodigoBarras is { } barras && barrasVistas.TryGetValue(barras, out var filaBarras))
                error = $"El código de barras '{barras}' está repetido (también en la fila {filaBarras}).";

            if (error is not null)
            {
                resultado.Errores.Add(new ErrorImportacion(fila.Numero, error));
                continue;
            }

            codigosVistos[producto!.Codigo] = fila.Numero;
            if (producto.CodigoBarras is not null) barrasVistas[producto.CodigoBarras] = fila.Numero;
            validas.Add(producto);
        }

        if (validas.Count > 0)
        {
            var carga = productos.CargarMasivo(validas, reloj.AhoraLocal());
            resultado.Insertados = carga.Insertados;
            resultado.Actualizados = carga.Actualizados;
            resultado.Errores.AddRange(carga.Errores);
        }

        resultado.Errores.Sort((a, b) => a.Fila.CompareTo(b.Fila));
        return resultado;
    }

    public byte[] Exportar() => exportador.Exportar(productos.ListarTodos(), reloj.AhoraLocal());

    public byte[] Plantilla() => exportador.Plantilla();

    private static (FilaProducto? Producto, string? Error) Convertir(FilaArchivo fila)
    {
        var codigo = Texto.Mayusculas(Valor(fila, ColCodigo));
        if (codigo.Length == 0) return (null, "Falta el código.");
        if (codigo.Length > 40) return (null, "El código supera los 40 caracteres.");

        var nombre = Texto.Mayusculas(Valor(fila, ColNombre));
        if (nombre.Length == 0) return (null, "Falta el nombre del producto.");
        if (nombre.Length > 200) return (null, "El nombre supera los 200 caracteres.");

        var textoPrecio = Valor(fila, ColPrecio);
        if (!LectorNumeros.Intentar(textoPrecio, out var precio) || precio < 0 || precio > Dinero.MontoMaximo)
            return (null, $"Precio no válido: '{textoPrecio}'.");

        var (stock, errorStock) = CantidadOpcional(fila, ColStock, "existencia");
        if (errorStock is not null) return (null, errorStock);

        // La columna "Conteo físico" del archivo exportado, si se llenó, reemplaza la existencia.
        var (conteo, errorConteo) = CantidadOpcional(fila, ColConteo, "conteo físico");
        if (errorConteo is not null) return (null, errorConteo);
        if (conteo is not null) stock = conteo;

        var (minimo, errorMinimo) = CantidadOpcional(fila, ColStockMinimo, "stock mínimo");
        if (errorMinimo is not null) return (null, errorMinimo);
        if (minimo < 0) return (null, "El stock mínimo no puede ser negativo.");

        var unidad = Texto.MayusculasONulo(Valor(fila, ColUnidad));
        if (unidad is { Length: > 12 }) return (null, "La unidad supera los 12 caracteres.");

        var categoria = Texto.MayusculasONulo(Valor(fila, ColCategoria));
        if (categoria is { Length: > 60 }) return (null, "La categoría supera los 60 caracteres.");

        var barras = Texto.MayusculasONulo(Valor(fila, ColBarras));
        if (barras is { Length: > 64 }) return (null, "El código de barras supera los 64 caracteres.");

        return (new FilaProducto
        {
            Fila = fila.Numero,
            Codigo = codigo,
            Nombre = nombre,
            Precio = Dinero.Redondear(precio),
            Stock = stock,
            StockMinimo = minimo,
            Unidad = unidad,
            Categoria = categoria,
            CodigoBarras = barras
        }, null);
    }

    private static (decimal? Valor, string? Error) CantidadOpcional(FilaArchivo fila, string[] alias, string campo)
    {
        var texto = Valor(fila, alias);
        if (string.IsNullOrWhiteSpace(texto)) return (null, null);
        if (!LectorNumeros.Intentar(texto, out var valor) || Math.Abs(valor) > Dinero.CantidadMaxima)
            return (null, $"Valor de {campo} no válido: '{texto}'.");
        return (Dinero.RedondearCantidad(valor), null);
    }

    private static string? Valor(FilaArchivo fila, string[] alias)
    {
        foreach (var nombre in alias)
        {
            if (fila.Celdas.TryGetValue(nombre, out var valor)) return valor;
        }
        return null;
    }
}
