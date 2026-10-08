using System.Text;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using static Facturacion.Web.Impresion.Html;

namespace Facturacion.Web.Impresion;

/// <summary>
/// Imprime SOLO los datos variables sobre el formulario preimpreso del talonario
/// (impresora de matriz de punto). Cada dato va en una caja en milímetros según la plantilla
/// configurada, más el desplazamiento de calibración; el texto se centra verticalmente en la caja.
/// </summary>
internal static class PlantillaPreimpreso
{
    /// <summary>Texto de la línea CLIENTE tal como se imprime.</summary>
    public static string TextoCliente(Factura f) =>
        f.ClienteRuc is { Length: > 0 } ruc ? $"{f.ClienteNombre}   RUC/CÉD.: {ruc}" : f.ClienteNombre;

    /// <summary>Descripción con la unidad cuando no es la predeterminada: "CABLE THHN #12 (METRO)".</summary>
    public static string TextoDescripcion(LineaFactura l) =>
        l.Unidad is "UND" or "" ? l.Descripcion : $"{l.Descripcion} ({l.Unidad})";

    public static string Generar(Factura f, AjustesImpresion imp, bool cuadricula = false)
    {
        var p = imp.Plantilla;
        var dx = imp.DesplazamientoXMm;
        var dy = imp.DesplazamientoYMm;

        var estilos = $$"""
            body { font-family: 'Courier New', Courier, monospace; font-size: {{Pt(p.TamanoFuentePt)}}; font-weight: 700; }
            .hoja { position: relative; width: {{Mm(p.AnchoPaginaMm)}}; height: {{Mm(p.AltoPaginaMm)}}; overflow: hidden; }
            .d { position: absolute; display: flex; align-items: center; white-space: nowrap; overflow: hidden; line-height: 1; }
            .cen { justify-content: center; }
            .der { justify-content: flex-end; }
            .linea-v { position: absolute; top: 0; bottom: 0; border-left: 1px solid #9ec5fe; }
            .linea-h { position: absolute; left: 0; right: 0; border-top: 1px solid #9ec5fe; }
            .linea-v.cada50, .linea-h.cada50 { border-color: #3b82f6; }
            .regla { position: absolute; font: 7pt Arial, sans-serif; color: #2563eb; }
            .caja-guia { position: absolute; outline: 1px dashed rgba(220, 38, 38, .55); }
            """;

        var c = new StringBuilder("<div class=\"hoja\">");
        if (cuadricula) Cuadricula(c, p);

        void Caja(decimal x, decimal y, decimal ancho, decimal alto, string texto, string clase = "")
        {
            var estilo = $"left:{Mm(x + dx)};top:{Mm(y + dy)};width:{Mm(ancho)};height:{Mm(alto)}";
            if (cuadricula) c.Append($"<div class=\"caja-guia\" style=\"{estilo}\"></div>");
            c.Append($"<div class=\"d {clase}\" style=\"{estilo}\">{E(texto)}</div>");
        }

        void Campo(Posicion pos, string texto, string clase = "") => Caja(pos.X, pos.Y, pos.Ancho, pos.Alto, texto, clase);

        Campo(p.Dia, f.Fecha.ToString("dd"), "cen");
        Campo(p.Mes, f.Fecha.ToString("MM"), "cen");
        Campo(p.Anio, f.Fecha.ToString("yyyy"), "cen");
        Campo(p.Cliente, TextoCliente(f));
        Campo(p.Direccion, f.ClienteDireccion ?? "");

        var filas = f.Lineas.Take(p.Filas.Maximo).ToList();
        for (var i = 0; i < filas.Count; i++)
        {
            var l = filas[i];
            var y = p.Filas.Y + i * p.Filas.Alto;
            Caja(p.Cantidad.X, y, p.Cantidad.Ancho, p.Filas.Alto, Dinero.Cantidad(l.Cantidad), "cen");
            Caja(p.Descripcion.X, y, p.Descripcion.Ancho, p.Filas.Alto, TextoDescripcion(l));
            Caja(p.PrecioUnitario.X, y, p.PrecioUnitario.Ancho, p.Filas.Alto, Dinero.Numero(l.PrecioUnitario), "der");
            Caja(p.Importe.X, y, p.Importe.Ancho, p.Filas.Alto, Dinero.Numero(l.Total), "der");
        }

        Campo(p.Total, Dinero.Numero(f.Total), "der");
        c.Append("</div>");
        return Documento($"Factura {f.NumeroCompleto} (talonario)", estilos, c.ToString());
    }

    /// <summary>Factura de prueba que llena todas las filas: así se ve si las últimas se corren.</summary>
    public static Factura FacturaDeEjemplo(DateTime fecha, int filas)
    {
        var lineas = Enumerable.Range(1, filas).Select(i => new LineaFactura
        {
            Cantidad = i,
            Descripcion = $"FILA {i} · PRUEBA DE ALINEACIÓN",
            Unidad = "UND",
            PrecioUnitario = 1234.50m,
            Total = 1234.50m * i
        }).ToList();
        return new Factura
        {
            Serie = "A",
            Fecha = fecha,
            ClienteNombre = "CLIENTE DE PRUEBA",
            ClienteDireccion = "DIRECCIÓN DE PRUEBA, MANAGUA",
            Lineas = lineas,
            Total = lineas.Sum(l => l.Total)
        };
    }

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
