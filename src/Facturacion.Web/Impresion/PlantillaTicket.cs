using System.Text;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using static Facturacion.Web.Impresion.Html;

namespace Facturacion.Web.Impresion;

/// <summary>Ticket para impresora térmica de 80 mm (o 58 mm).</summary>
internal static class PlantillaTicket
{
    /// <summary>Estilos según el ancho del rollo (el área imprimible es ~72 mm en 80 mm y ~48 mm en 58 mm).</summary>
    public static string Estilos(int anchoRolloMm)
    {
        var ancho = anchoRolloMm == 58 ? 48 : 72;
        var fuente = anchoRolloMm == 58 ? "8pt" : "9pt";
        return $$"""
            body { font-family: Arial, Helvetica, sans-serif; font-size: {{fuente}}; line-height: 1.25; }
            .hoja { width: {{ancho}}mm; padding: 2mm 1mm 4mm; }
            .c { text-align: center; }
            .n { font-weight: 700; }
            .g { font-size: 1.4em; font-weight: 800; }
            .p { font-size: .9em; }
            hr { border: none; border-top: 1px dashed #000; margin: 1.6mm 0; }
            .fila { display: flex; justify-content: space-between; gap: 2mm; }
            .fila span:last-child { text-align: right; }
            .item { margin-bottom: 1mm; }
            .total { font-size: 1.35em; font-weight: 800; margin: .8mm 0; }
            """;
    }

    public static string Generar(Factura f, Ajustes ajustes, bool reimpresion)
    {
        var n = ajustes.Negocio;
        var t = new StringBuilder("<div class=\"hoja\">");
        t.Append($"<div class=\"c g\">{E(n.NombreComercial)}</div>");
        if (n.Giro.Length > 0) t.Append($"<div class=\"c n\">{E(n.Giro)}</div>");
        if (n.Propietario.Length > 0) t.Append($"<div class=\"c p\">{E(n.Propietario)}</div>");
        if (n.Ruc.Length > 0) t.Append($"<div class=\"c p\">RUC: {E(n.Ruc)}</div>");
        if (n.Direccion.Length > 0) t.Append($"<div class=\"c p\">{E(n.Direccion)}</div>");
        if (n.Telefono.Length > 0) t.Append($"<div class=\"c p\">Tel.: {E(n.Telefono)}</div>");
        t.Append("<hr>");

        t.Append($"<div class=\"c n\">FACTURA {E(ajustes.Facturacion.TipoFactura)} No. {E(f.NumeroCompleto)}</div>");
        if (reimpresion) t.Append("<div class=\"c\">*** REIMPRESIÓN ***</div>");
        if (f.Estado == EstadoFactura.Anulada) t.Append("<div class=\"c n\">*** FACTURA ANULADA ***</div>");
        t.Append($"<div class=\"fila\"><span>Fecha:</span><span>{f.Fecha:dd/MM/yyyy HH:mm}</span></div>");
        t.Append($"<div class=\"fila\"><span>Cliente:</span><span>{E(f.ClienteNombre)}</span></div>");
        if (f.ClienteRuc is { Length: > 0 }) t.Append($"<div class=\"fila\"><span>RUC/Céd.:</span><span>{E(f.ClienteRuc)}</span></div>");
        t.Append("<hr>");

        foreach (var l in f.Lineas)
        {
            t.Append($"""
                <div class="item"><div>{E(l.Descripcion)}</div>
                <div class="fila"><span>{Dinero.Cantidad(l.Cantidad)} {E(l.Unidad)} x {Dinero.Numero(l.PrecioUnitario)}</span><span>{Dinero.Numero(l.Total)}</span></div></div>
                """);
        }
        t.Append("<hr>");

        if (f.Iva != 0)
        {
            t.Append($"<div class=\"fila\"><span>Subtotal</span><span>{Dinero.Formatear(f.Subtotal)}</span></div>");
            t.Append($"<div class=\"fila\"><span>IVA</span><span>{Dinero.Formatear(f.Iva)}</span></div>");
        }
        t.Append($"<div class=\"fila total\"><span>TOTAL</span><span>{Dinero.Formatear(f.Total)}</span></div>");
        t.Append($"<div class=\"fila\"><span>Pago:</span><span>{f.MetodoPago}</span></div>");
        if (f.MetodoPago == MetodoPago.Efectivo)
        {
            t.Append($"<div class=\"fila\"><span>Recibido:</span><span>{Dinero.Formatear(f.MontoRecibido ?? f.Total)}</span></div>");
            t.Append($"<div class=\"fila n\"><span>Vuelto:</span><span>{Dinero.Formatear(f.Vuelto ?? 0)}</span></div>");
        }
        else if (f.Referencia is { Length: > 0 })
        {
            t.Append($"<div class=\"fila\"><span>Referencia:</span><span>{E(f.Referencia)}</span></div>");
        }

        t.Append("<hr>");
        t.Append($"<div class=\"c p\">Artículos: {f.Lineas.Count}</div>");
        if (n.MensajePie.Length > 0) t.Append($"<div class=\"c n\">{E(n.MensajePie)}</div>");
        t.Append("</div>");

        return Documento($"Ticket {f.NumeroCompleto}", Estilos(ajustes.Impresion.AnchoTicketMm), t.ToString());
    }
}
