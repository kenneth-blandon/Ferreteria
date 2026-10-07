using System.Globalization;
using ClosedXML.Excel;
using Facturacion.Core.Importacion;

namespace Facturacion.Datos.Archivos;

/// <summary>Lee la primera hoja de un .xlsx (no requiere Microsoft Office instalado).</summary>
public sealed class LectorExcel : ILectorArchivo
{
    public IReadOnlyCollection<string> Extensiones { get; } = [".xlsx", ".xlsm"];

    public IReadOnlyList<FilaArchivo> Leer(Stream contenido)
    {
        using var libro = new XLWorkbook(Buscable(contenido));
        var hoja = libro.Worksheets.First();
        var rango = hoja.RangeUsed();
        if (rango is null) return [];

        var encabezados = rango.FirstRow().Cells()
            .Select(c => (Columna: c.Address.ColumnNumber, Nombre: ServicioImportacion.NormalizarEncabezado(TextoCelda(c))))
            .Where(e => e.Nombre.Length > 0)
            .ToList();

        var filas = new List<FilaArchivo>();
        foreach (var fila in rango.RowsUsed().Skip(1))
        {
            var celdas = new Dictionary<string, string>();
            foreach (var (columna, nombre) in encabezados)
                celdas.TryAdd(nombre, TextoCelda(hoja.Cell(fila.RowNumber(), columna)).Trim());
            filas.Add(new FilaArchivo(fila.RowNumber(), celdas));
        }
        return filas;
    }

    private static string TextoCelda(IXLCell celda)
    {
        var valor = celda.Value;
        return valor.Type switch
        {
            XLDataType.Blank => "",
            // Los números se pasan en formato invariante ("1250.5"); un código numérico 123 queda "123".
            XLDataType.Number => valor.GetNumber().ToString("0.############", CultureInfo.InvariantCulture),
            XLDataType.Text => valor.GetText(),
            XLDataType.Boolean => valor.GetBoolean() ? "1" : "0",
            XLDataType.DateTime => valor.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => celda.GetFormattedString()
        };
    }

    private static Stream Buscable(Stream contenido)
    {
        if (contenido.CanSeek) return contenido;
        var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        memoria.Position = 0;
        return memoria;
    }
}
