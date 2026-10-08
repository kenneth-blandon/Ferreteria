using System.Text;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using Facturacion.Core.Reportes;
using static Facturacion.Web.Impresion.Html;

namespace Facturacion.Web.Impresion;

/// <summary>Reporte de cierre de caja en hoja carta.</summary>
internal static class PlantillaCierre
{
    private const string Estilos = """
        body { font-family: Arial, Helvetica, sans-serif; font-size: 10pt; }
        .hoja { width: 216mm; min-height: 279mm; padding: 14mm 16mm; }
        h1 { font-size: 16pt; margin: 0; }
        h2 { font-size: 11pt; margin: 6mm 0 2mm; border-bottom: .3mm solid #000; padding-bottom: 1mm; }
        .sub { color: #333; }
        .cifras { display: grid; grid-template-columns: repeat(4, 1fr); gap: 3mm; margin-top: 5mm; }
        .cifra { border: .3mm solid #000; padding: 2.5mm; }
        .cifra b { display: block; font-size: 13pt; margin-top: 1mm; }
        table { width: 100%; border-collapse: collapse; }
        th, td { border-bottom: .2mm solid #999; padding: 1.2mm 1.5mm; text-align: left; font-size: 9pt; }
        th { background: #e6e6e6; }
        .num { text-align: right; white-space: nowrap; }
        tr.anulada td { color: #c1121f; text-decoration: line-through; }
        .firmas { display: grid; grid-template-columns: 1fr 1fr; gap: 24mm; margin: 18mm 10mm 0; text-align: center; font-weight: 700; font-size: 9pt; }
        .firmas div { border-top: .3mm solid #000; padding-top: 1.5mm; }
        """;

    public static string Generar(ResumenCierre r, Ajustes ajustes, DateTime impreso)
    {
        var c = new StringBuilder("<div class=\"hoja\">");
        c.Append($"""
            <h1>CIERRE DE CAJA · {r.Fecha:dd/MM/yyyy}</h1>
            <div class="sub">{E(ajustes.Negocio.NombreComercial)} · Impreso el {impreso:dd/MM/yyyy HH:mm}</div>
            <div class="cifras">
              <div class="cifra">Total vendido<b>{Dinero.Formatear(r.TotalVendido)}</b></div>
              <div class="cifra">Facturas emitidas<b>{r.FacturasEmitidas}</b></div>
              <div class="cifra">Facturas anuladas<b>{r.FacturasAnuladas}</b></div>
              <div class="cifra">Numeración usada<b>{Rango(r, ajustes)}</b></div>
            </div>
            <h2>Ventas por forma de pago</h2>
            <table><tr><th>Forma de pago</th><th class="num">Facturas</th><th class="num">Total</th></tr>
            """);
        foreach (var m in r.PorMetodo)
            c.Append($"<tr><td>{m.Metodo}</td><td class=\"num\">{m.Facturas}</td><td class=\"num\">{Dinero.Formatear(m.Total)}</td></tr>");
        c.Append($"<tr><th>TOTAL</th><th class=\"num\">{r.FacturasEmitidas}</th><th class=\"num\">{Dinero.Formatear(r.TotalVendido)}</th></tr></table>");
        if (r.Iva != 0)
            c.Append($"<p>Subtotal {Dinero.Formatear(r.Subtotal)} · IVA {Dinero.Formatear(r.Iva)}</p>");

        if (r.ProductosMasVendidos.Count > 0)
        {
            c.Append("<h2>Productos más vendidos</h2><table><tr><th>Código</th><th>Descripción</th><th class=\"num\">Cantidad</th><th class=\"num\">Total</th></tr>");
            foreach (var p in r.ProductosMasVendidos)
                c.Append($"<tr><td>{E(p.Codigo)}</td><td>{E(p.Descripcion)}</td><td class=\"num\">{Dinero.Cantidad(p.Cantidad)} {E(p.Unidad)}</td><td class=\"num\">{Dinero.Formatear(p.Total)}</td></tr>");
            c.Append("</table>");
        }

        if (r.Facturas.Count > 0)
        {
            c.Append("<h2>Facturas del día</h2><table><tr><th>No.</th><th>Hora</th><th>Cliente</th><th>Pago</th><th class=\"num\">Total</th><th>Estado</th></tr>");
            foreach (var f in r.Facturas)
            {
                var anulada = f.Estado == EstadoFactura.Anulada;
                var estado = f.Modificada && !anulada ? "Emitida (modificada)" : f.Estado.ToString();
                c.Append($"<tr{(anulada ? " class=\"anulada\"" : "")}><td>{E(f.NumeroCompleto)}</td><td>{f.Fecha:HH:mm}</td><td>{E(f.ClienteNombre)}</td>" +
                         $"<td>{f.MetodoPago}</td><td class=\"num\">{Dinero.Formatear(f.Total)}</td><td>{estado}</td></tr>");
            }
            c.Append("</table>");
        }

        c.Append("<div class=\"firmas\"><div>ENTREGADO POR</div><div>RECIBIDO POR</div></div></div>");
        return Documento($"Cierre de caja {r.Fecha:dd-MM-yyyy}", Estilos, c.ToString());
    }

    private static string Rango(ResumenCierre r, Ajustes ajustes) =>
        r.PrimerNumero is { } desde && r.UltimoNumero is { } hasta
            ? (desde == hasta ? $"{ajustes.Facturacion.Serie}-{desde}" : $"{desde} – {hasta}")
            : "—";
}
