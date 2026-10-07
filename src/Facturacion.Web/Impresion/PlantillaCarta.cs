using System.Text;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using static Facturacion.Web.Impresion.Html;

namespace Facturacion.Web.Impresion;

/// <summary>Factura completa en hoja carta (impresora de inyección o láser), con el diseño del talonario.</summary>
internal static class PlantillaCarta
{
    private const int FilasMinimas = 14;

    private const string Estilos = """
        body { font-family: Arial, Helvetica, sans-serif; font-size: 10pt; }
        .hoja { width: 216mm; min-height: 279mm; padding: 11mm 12mm 10mm; position: relative; }
        .cabecera { display: flex; align-items: flex-start; gap: 5mm; border-bottom: .6mm solid #000; padding-bottom: 3mm; }
        .doc { width: 46mm; }
        .doc .tipo { font-size: 16pt; font-weight: 800; letter-spacing: .3mm; }
        .doc .sub { font-size: 9.5pt; font-weight: 700; }
        .doc .numero { font-size: 17pt; font-weight: 800; color: #c1121f; margin-top: 1mm; }
        .doc .marca { margin-top: 1mm; font-size: 8pt; font-weight: 700; border: .3mm solid #000; display: inline-block; padding: .5mm 1.5mm; }
        .negocio { flex: 1; text-align: center; }
        .negocio .giro { font-family: Georgia, 'Times New Roman', serif; font-size: 17pt; line-height: 1.1; }
        .negocio .nombre { font-family: Georgia, 'Times New Roman', serif; font-size: 22pt; font-weight: 700; line-height: 1.15; }
        .negocio .dato { font-size: 8.5pt; color: #222; }
        .negocio .ruc { font-size: 9pt; font-weight: 700; }
        table.fecha { border-collapse: collapse; font-size: 8pt; text-align: center; }
        table.fecha th, table.fecha td { border: .3mm solid #000; width: 11mm; padding: 1mm .5mm; }
        table.fecha td { font-size: 10pt; font-weight: 700; height: 7mm; }
        .cliente { display: grid; grid-template-columns: 1fr 52mm; gap: 1.5mm 6mm; margin-top: 3.5mm; }
        .campo { border-bottom: .3mm solid #444; padding: .8mm 0; min-height: 6mm; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
        .etq { font-weight: 700; font-size: 8.5pt; margin-right: 2mm; }
        table.lineas { width: 100%; border-collapse: collapse; margin-top: 4mm; }
        table.lineas thead { display: table-header-group; }
        table.lineas th { border: .3mm solid #000; background: #e6e6e6; font-size: 8.5pt; padding: 1.4mm; }
        table.lineas td { border-left: .3mm solid #000; border-right: .3mm solid #000; padding: 1.1mm 1.5mm; height: 6.4mm; font-size: 9.5pt; }
        table.lineas tbody tr:last-child td { border-bottom: .3mm solid #000; }
        table.lineas tr { page-break-inside: avoid; }
        .cant { width: 20mm; text-align: center; }
        .pu, .tot { width: 28mm; text-align: right; white-space: nowrap; }
        .unidad { color: #555; font-size: 8pt; }
        .pie-tabla { display: flex; gap: 6mm; align-items: flex-start; }
        .letras { flex: 1; font-size: 8.5pt; padding-top: 1.5mm; }
        .totales { width: 70mm; border: .3mm solid #000; border-top: none; }
        .totales div { display: flex; justify-content: space-between; padding: 1.2mm 2mm; }
        .totales .total { font-weight: 800; font-size: 11.5pt; background: #e6e6e6; }
        .pago { margin-top: 2mm; font-size: 8.5pt; color: #222; }
        .condiciones { margin-top: 4mm; font-size: 7.5pt; line-height: 1.4; color: #222; }
        .firmas { display: grid; grid-template-columns: 1fr 1fr; gap: 24mm; margin: 16mm 10mm 0; text-align: center; font-size: 8.5pt; font-weight: 700; }
        .firmas div { border-top: .3mm solid #000; padding-top: 1.5mm; }
        .sello { position: absolute; top: 105mm; left: 0; right: 0; text-align: center; font-size: 80pt; font-weight: 900;
                 color: rgba(193, 18, 31, .2); transform: rotate(-24deg); pointer-events: none; }
        .motivo { margin-top: 4mm; font-size: 8.5pt; font-weight: 700; color: #c1121f; }
        """;

    public static string Generar(Factura f, Ajustes ajustes, bool reimpresion)
    {
        var n = ajustes.Negocio;
        var cuerpo = new StringBuilder();
        cuerpo.Append("<div class=\"hoja\">");
        if (f.Estado == EstadoFactura.Anulada) cuerpo.Append("<div class=\"sello\">ANULADA</div>");

        // Encabezado: tipo y número, datos del negocio, fecha.
        cuerpo.Append($"""
            <header class="cabecera">
              <div class="doc">
                <div class="tipo">FACTURA</div>
                <div class="sub">{E(ajustes.Facturacion.TipoFactura)}</div>
                <div class="sub">SERIE "{E(f.Serie)}"</div>
                <div class="numero">No. {f.Numero}</div>
                {(reimpresion ? "<div class=\"marca\">REIMPRESIÓN</div>" : "")}
              </div>
              <div class="negocio">
                <div class="giro">{E(n.Giro)}</div>
                <div class="nombre">{E(n.NombreComercial)}</div>
                <div class="dato">{E(n.Propietario)}</div>
                <div class="dato">{E(n.Direccion)}</div>
                <div class="dato">{E(DatosContacto(n))}</div>
                {(n.Ruc.Length > 0 ? $"<div class=\"ruc\">RUC: {E(n.Ruc)}</div>" : "")}
              </div>
              <table class="fecha">
                <tr><th>DÍA</th><th>MES</th><th>AÑO</th></tr>
                <tr><td>{f.Fecha:dd}</td><td>{f.Fecha:MM}</td><td>{f.Fecha:yyyy}</td></tr>
              </table>
            </header>
            <section class="cliente">
              <div class="campo"><span class="etq">CLIENTE:</span>{E(f.ClienteNombre)}</div>
              <div class="campo"><span class="etq">RUC/CÉD.:</span>{E(f.ClienteRuc)}</div>
              <div class="campo"><span class="etq">DIRECCIÓN:</span>{E(f.ClienteDireccion)}</div>
              <div class="campo"><span class="etq">HORA:</span>{f.Fecha:HH:mm}</div>
            </section>
            """);

        // Detalle con filas vacías de relleno para conservar el aspecto del talonario.
        cuerpo.Append("""
            <table class="lineas">
              <thead><tr><th class="cant">CANT.</th><th>DESCRIPCIÓN</th><th class="pu">P. UNIT.</th><th class="tot">TOTAL</th></tr></thead>
              <tbody>
            """);
        foreach (var l in f.Lineas)
        {
            cuerpo.Append($"""
                <tr><td class="cant">{Dinero.Cantidad(l.Cantidad)} <span class="unidad">{E(l.Unidad)}</span></td>
                <td>{E(l.Descripcion)}</td><td class="pu">{Dinero.Numero(l.PrecioUnitario)}</td><td class="tot">{Dinero.Numero(l.Total)}</td></tr>
                """);
        }
        for (var i = f.Lineas.Count; i < FilasMinimas; i++)
            cuerpo.Append("<tr><td class=\"cant\">&nbsp;</td><td></td><td class=\"pu\"></td><td class=\"tot\"></td></tr>");
        cuerpo.Append("</tbody></table>");

        // Totales y monto en letras.
        cuerpo.Append($"""
            <div class="pie-tabla">
              <div class="letras"><b>SON:</b> {E(NumeroALetras.Monto(f.Total))}</div>
              <div class="totales">
                {(f.Iva != 0 ? $"<div><span>SUBTOTAL</span><span>{Dinero.Formatear(f.Subtotal)}</span></div>" : "")}
                {(f.Iva != 0 ? $"<div><span>IVA {ajustes.Facturacion.IvaPorcentaje:0.##}%</span><span>{Dinero.Formatear(f.Iva)}</span></div>" : "")}
                <div class="total"><span>TOTAL</span><span>{Dinero.Formatear(f.Total)}</span></div>
              </div>
            </div>
            <div class="pago">{E(DescripcionPago(f))}</div>
            """);

        if (n.Condiciones.Length > 0)
            cuerpo.Append($"<div class=\"condiciones\"><b>PAGARÉ A LA ORDEN</b><br>{Multilinea(n.Condiciones)}</div>");

        cuerpo.Append("<div class=\"firmas\"><div>RECIBÍ CONFORME</div><div>ENTREGUÉ CONFORME</div></div>");

        if (f.Estado == EstadoFactura.Anulada)
            cuerpo.Append($"<div class=\"motivo\">FACTURA ANULADA el {f.FechaAnulacion:dd/MM/yyyy HH:mm}. Motivo: {E(f.MotivoAnulacion)}</div>");

        cuerpo.Append("</div>");
        return Documento($"Factura {f.NumeroCompleto}", Estilos, cuerpo.ToString());
    }

    internal static string DatosContacto(DatosNegocio n) =>
        string.Join(" · ", new[] { n.Telefono.Length > 0 ? "Tel.: " + n.Telefono : "", n.Email }.Where(x => x.Length > 0));

    internal static string DescripcionPago(Factura f) => f.MetodoPago switch
    {
        MetodoPago.Efectivo => $"Forma de pago: EFECTIVO · Recibido: {Dinero.Formatear(f.MontoRecibido ?? f.Total)} · Vuelto: {Dinero.Formatear(f.Vuelto ?? 0)}",
        _ => $"Forma de pago: {f.MetodoPago.ToString().ToUpperInvariant()}" + (f.Referencia is { Length: > 0 } r ? $" · Referencia: {r}" : "")
    };
}
