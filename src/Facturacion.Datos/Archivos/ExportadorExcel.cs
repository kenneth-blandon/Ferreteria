using ClosedXML.Excel;
using Facturacion.Core.Catalogo;
using Facturacion.Core.Importacion;

namespace Facturacion.Datos.Archivos;

/// <summary>
/// Exporta el inventario a Excel. El archivo se puede volver a importar: la columna
/// "Conteo físico", si se llena, reemplaza la existencia al importar.
/// </summary>
public sealed class ExportadorExcel : IExportadorInventario
{
    private static readonly string[] Encabezados =
        ["Código", "Nombre", "Categoría", "Unidad", "Precio", "Stock", "Stock mínimo", "Código de barras", "Conteo físico"];

    private static readonly double[] Anchos = [14, 48, 18, 9, 12, 10, 13, 18, 14];

    public byte[] Exportar(IReadOnlyList<Producto> productos, DateTime fecha)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Inventario");
        EscribirEncabezado(hoja);

        var fila = 2;
        foreach (var p in productos)
        {
            hoja.Cell(fila, 1).SetValue(p.Codigo);
            hoja.Cell(fila, 2).SetValue(p.Nombre);
            hoja.Cell(fila, 3).SetValue(p.Categoria ?? "");
            hoja.Cell(fila, 4).SetValue(p.Unidad);
            hoja.Cell(fila, 5).SetValue(p.Precio);
            hoja.Cell(fila, 6).SetValue(p.Stock);
            hoja.Cell(fila, 7).SetValue(p.StockMinimo);
            hoja.Cell(fila, 8).SetValue(p.CodigoBarras ?? "");
            if (p.BajoStock) hoja.Range(fila, 1, fila, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
            fila++;
        }

        DarFormato(hoja, fila - 1);
        // La fecha va en el encabezado de impresión (no en una fila) para que el archivo se pueda reimportar.
        hoja.PageSetup.Header.Center.AddText($"Inventario al {fecha:dd/MM/yyyy HH:mm} · {productos.Count} productos");
        hoja.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.SetRowsToRepeatAtTop(1, 1);
        return Guardar(libro);
    }

    public byte[] Plantilla()
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Productos");
        EscribirEncabezado(hoja);

        object[][] ejemplos =
        [
            ["ELE-001", "Cable THHN #12 AWG negro", "Eléctricos", "MTS", 28.50m, 500m, 100m, ""],
            ["FON-001", "Tubo PVC 1/2 pulgada x 6 m", "Fontanería", "UND", 95m, 40m, 10m, ""],
            ["HER-001", "Martillo de uña 16 oz", "Herramientas", "UND", 320m, 12m, 3m, "7501234567890"]
        ];
        for (var i = 0; i < ejemplos.Length; i++)
            for (var j = 0; j < ejemplos[i].Length; j++)
                hoja.Cell(i + 2, j + 1).Value = XLCellValue.FromObject(ejemplos[i][j]);

        DarFormato(hoja, ejemplos.Length + 1);

        // Las instrucciones van en otra hoja: solo se importa la primera.
        var ayuda = libro.AddWorksheet("Instrucciones");
        var notas = new[]
        {
            "Cómo llenar la hoja 'Productos':",
            "• Obligatorias: Código, Nombre y Precio. Las demás columnas son opcionales.",
            "• Si el código ya existe, el producto se actualiza; si no, se crea.",
            "• Una celda vacía conserva el valor actual del producto (por ejemplo, el stock).",
            "• Las categorías que no existan se crean automáticamente.",
            "• 'Conteo físico': si se llena, reemplaza la existencia al importar.",
            "• Reemplace las filas de ejemplo con sus productos."
        };
        for (var i = 0; i < notas.Length; i++)
            ayuda.Cell(i + 1, 1).SetValue(notas[i]);
        ayuda.Cell(1, 1).Style.Font.Bold = true;
        ayuda.Column(1).Width = 90;
        return Guardar(libro);
    }

    private static void EscribirEncabezado(IXLWorksheet hoja)
    {
        for (var i = 0; i < Encabezados.Length; i++)
        {
            hoja.Cell(1, i + 1).SetValue(Encabezados[i]);
            hoja.Column(i + 1).Width = Anchos[i];
        }
        var encabezado = hoja.Range(1, 1, 1, Encabezados.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
        hoja.SheetView.FreezeRows(1);
    }

    private static void DarFormato(IXLWorksheet hoja, int ultimaFila)
    {
        if (ultimaFila < 2) return;
        hoja.Range(2, 5, ultimaFila, 5).Style.NumberFormat.Format = "#,##0.00";
        hoja.Range(2, 6, ultimaFila, 7).Style.NumberFormat.Format = "#,##0.###";
        hoja.Range(2, 1, ultimaFila, 1).Style.NumberFormat.Format = "@";
        hoja.Range(2, 8, ultimaFila, 8).Style.NumberFormat.Format = "@";
        hoja.Range(1, 1, ultimaFila, Encabezados.Length).SetAutoFilter();
    }

    private static byte[] Guardar(XLWorkbook libro)
    {
        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
