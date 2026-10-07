using System.Text;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using static Facturacion.Web.Impresion.Html;

namespace Facturacion.Web.Impresion;

/// <summary>
/// Imprime SOLO los datos variables sobre el formulario preimpreso del talonario
/// (impresora de matriz de punto con papel autocopiante). Cada dato se ubica en milímetros
/// según la plantilla configurada más el desplazamiento de calibración.
/// </summary>
internal static class PlantillaPreimpreso
{
    public static string Generar(Factura f, AjustesImpresion imp, bool cuadricula = false)
    {
        var p = imp.Plantilla;
        var dx = imp.DesplazamientoXMm;
        var dy = imp.DesplazamientoYMm;

        var estilos = $$"""
            body { font-family: 'Courier New', Courier, monospace; font-size: {{Pt(p.TamanoFuentePt)}}; font-weight: 700; }
            .hoja { position: relative; width: {{Mm(p.AnchoPaginaMm)}}; height: {{Mm(p.AltoPaginaMm)}}; overflow: hidden; }
            .d { position: absolute; white-space: nowrap; overflow: hidden; line-height: 1.15; }
            .der { text-align: right; }
            .cen { text-align: center; }
            .linea-v { position: absolute; top: 0; bottom: 0; border-left: 1px solid #9ec5fe; }
            .linea-h { position: absolute; left: 0; right: 0; border-top: 1px solid #9ec5fe; }
            .linea-v.cada50, .linea-h.cada50 { border-color: #3b82f6; }
            .regla { position: absolute; font: 7pt Arial, sans-serif; color: #2563eb; }
            """;

        var c = new StringBuilder("<div class=\"hoja\">");
        if (cuadricula) Cuadricula(c, p);

        void Dato(decimal x, decimal y, decimal ancho, string texto, string clase = "") =>
            c.Append($"<div class=\"d {clase}\" style=\"left:{Mm(x + dx)};top:{Mm(y + dy)};width:{Mm(ancho)}\">{E(texto)}</div>");

        Dato(p.Dia.X, p.Dia.Y, p.Dia.Ancho, f.Fecha.ToString("dd"), "cen");
        Dato(p.Mes.X, p.Mes.Y, p.Mes.Ancho, f.Fecha.ToString("MM"), "cen");
        Dato(p.Anio.X, p.Anio.Y, p.Anio.Ancho, f.Fecha.ToString("yyyy"), "cen");
        Dato(p.Cliente.X, p.Cliente.Y, p.Cliente.Ancho,
            f.ClienteRuc is { Length: > 0 } ruc ? $"{f.ClienteNombre}  RUC: {ruc}" : f.ClienteNombre);
        Dato(p.Direccion.X, p.Direccion.Y, p.Direccion.Ancho, f.ClienteDireccion ?? "");

        var filas = f.Lineas.Take(p.Filas.Maximo).ToList();
        for (var i = 0; i < filas.Count; i++)
        {
            var l = filas[i];
            var y = p.Filas.Y + i * p.Filas.Alto;
            Dato(p.Cantidad.X, y, p.Cantidad.Ancho, Dinero.Cantidad(l.Cantidad), "cen");
            Dato(p.Descripcion.X, y, p.Descripcion.Ancho, l.Unidad is "UND" or "" ? l.Descripcion : $"{l.Descripcion} ({l.Unidad})");
            Dato(p.PrecioUnitario.X, y, p.PrecioUnitario.Ancho, Dinero.Numero(l.PrecioUnitario), "der");
            Dato(p.Importe.X, y, p.Importe.Ancho, Dinero.Numero(l.Total), "der");
        }

        Dato(p.Total.X, p.Total.Y, p.Total.Ancho, Dinero.Numero(f.Total), "der");
        c.Append("</div>");
        return Documento($"Factura {f.NumeroCompleto} (preimpreso)", estilos, c.ToString());
    }

    /// <summary>Factura de ejemplo para la hoja de calibración.</summary>
    public static Factura FacturaDeEjemplo(DateTime fecha) => new()
    {
        Serie = "A",
        Numero = 0,
        Fecha = fecha,
        ClienteNombre = "CLIENTE DE PRUEBA",
        ClienteDireccion = "DIRECCIÓN DE PRUEBA, MANAGUA",
        Lineas =
        [
            new LineaFactura { Cantidad = 1, Descripcion = "PRIMERA LÍNEA DE PRUEBA", Unidad = "UND", PrecioUnitario = 1234.50m, Total = 1234.50m },
            new LineaFactura { Cantidad = 12.5m, Descripcion = "SEGUNDA LÍNEA", Unidad = "MTS", PrecioUnitario = 28.50m, Total = 356.25m },
            new LineaFactura { Cantidad = 100, Descripcion = "TERCERA LÍNEA", Unidad = "UND", PrecioUnitario = 2m, Total = 200m }
        ],
        Total = 1790.75m
    };

    /// <summary>Cuadrícula milimétrica para medir la posición real de cada campo del formulario.</summary>
    private static void Cuadricula(StringBuilder c, PlantillaPreimpresa p)
    {
        for (var x = 10; x < p.AnchoPaginaMm; x += 10)
        {
            c.Append($"<div class=\"linea-v{(x % 50 == 0 ? " cada50" : "")}\" style=\"left:{x}mm\"></div>");
            c.Append($"<div class=\"regla\" style=\"left:{x + 0.5m}mm;top:1mm\">{x}</div>");
        }
        for (var y = 10; y < p.AltoPaginaMm; y += 10)
        {
            c.Append($"<div class=\"linea-h{(y % 50 == 0 ? " cada50" : "")}\" style=\"top:{y}mm\"></div>");
            c.Append($"<div class=\"regla\" style=\"left:1mm;top:{y + 0.5m}mm\">{y}</div>");
        }
    }
}
