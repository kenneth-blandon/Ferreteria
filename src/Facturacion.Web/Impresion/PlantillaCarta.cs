using System.Text;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using static Facturacion.Web.Impresion.Html;

namespace Facturacion.Web.Impresion;

/// <summary>
/// Vista previa en hoja carta: reproduce el talonario con los datos de la factura para que el usuario
/// vea cómo quedará. No se envía a la impresora (allí solo se imprimen los datos sobre el formulario);
/// también se usa para guardar la factura en PDF.
/// </summary>
internal static class PlantillaCarta
{
    private const string Estilos = """
        body { font-family: Arial, Helvetica, sans-serif; font-size: 9.5pt; }
        .hoja { width: 216mm; min-height: 279mm; padding: 9mm 9mm 8mm; position: relative; }
        .cabecera { display: grid; grid-template-columns: 48mm 1fr 34mm; gap: 3mm; align-items: start; }
        .doc { text-align: center; }
        .doc .tipo { font-size: 17pt; font-weight: 900; letter-spacing: .2mm; color: #333; }
        .doc .sub { font-size: 11pt; font-weight: 700; color: #444; line-height: 1.15; }
        .doc .numero { font-family: 'Courier New', monospace; font-size: 17pt; color: #c1121f; margin: 1mm 0 2mm; letter-spacing: .5mm; }
        table.fecha { border-collapse: separate; border-spacing: 0; margin: 0 auto; border: .35mm solid #555; border-radius: 3mm; overflow: hidden; }
        table.fecha th { font-size: 7pt; color: #555; padding: .6mm 2mm; border-bottom: .3mm solid #555; }
        table.fecha td { font-family: 'Courier New', monospace; font-weight: 700; font-size: 11pt; height: 7mm; width: 11mm; text-align: center; }
        table.fecha th + th, table.fecha td + td { border-left: .3mm solid #555; }
        .negocio { text-align: center; }
        .negocio .giro { font-family: Georgia, 'Times New Roman', serif; font-size: 21pt; line-height: 1.05; color: #333; }
        .negocio .nombre { font-family: Georgia, 'Times New Roman', serif; font-size: 21pt; line-height: 1.05; color: #333; }
        .negocio .propietario { font-family: Georgia, 'Times New Roman', serif; font-size: 12pt; color: #444; margin-bottom: 1mm; }
        .negocio .dato { font-size: 8.5pt; font-weight: 700; color: #444; line-height: 1.25; }
        .marcas { display: flex; flex-direction: column; gap: 1.5mm; align-items: flex-end; padding-top: 2mm; }
        .marca { font-size: 7.5pt; font-weight: 800; border: .35mm solid #333; border-radius: 1mm; padding: .6mm 1.6mm; }
        .marca.roja { color: #c1121f; border-color: #c1121f; }
        .linea-dato { display: flex; align-items: flex-end; gap: 1.5mm; margin-top: 3mm; }
        .linea-dato .etq { font-weight: 700; color: #444; font-size: 8.5pt; white-space: nowrap; }
        .linea-dato .valor { flex: 1; border-bottom: .3mm solid #555; font-family: 'Courier New', monospace; font-weight: 700;
                             font-size: 10pt; min-height: 5mm; padding-left: 1mm; white-space: nowrap; overflow: hidden; }
        table.lineas { width: 198mm; margin-top: 2.5mm; border-collapse: separate; border-spacing: 0;
                       border: .35mm solid #555; border-radius: 3mm; overflow: hidden; }
        table.lineas th { font-size: 8.5pt; color: #444; height: 7mm; border-bottom: .3mm solid #555; }
        table.lineas td { height: 6.16mm; padding: 0 1.5mm; border-bottom: .2mm solid #888; font-family: 'Courier New', monospace;
                          font-weight: 700; font-size: 9.5pt; white-space: nowrap; overflow: hidden; }
        table.lineas th + th, table.lineas td + td { border-left: .3mm solid #555; }
        table.lineas tfoot td { border-bottom: none; border-top: .3mm solid #555; }
        .cen { text-align: center; }
        .num { text-align: right; }
        .etq-total { text-align: right; font-family: Arial, sans-serif !important; color: #444; }
        .pagare { display: grid; grid-template-columns: 1fr auto 1fr; margin-top: 2mm; font-weight: 700; color: #444; }
        .pagare span:nth-child(2) { font-size: 10pt; }
        .pagare .cuota { text-align: right; font-size: 9.5pt; }
        .condiciones { margin-top: 2mm; font-size: 7pt; line-height: 1.55; color: #444; }
        .firmas { display: grid; grid-template-columns: 1fr 1fr; gap: 40mm; margin: 14mm 6mm 0; text-align: center; font-weight: 700; color: #444; }
        .firmas div { border-top: .3mm solid #555; padding-top: 1mm; }
        .sello { position: absolute; top: 120mm; left: 0; right: 0; text-align: center; font-size: 80pt; font-weight: 900;
                 color: rgba(193, 18, 31, .18); transform: rotate(-24deg); pointer-events: none; }
        .nota { margin-top: 4mm; font-size: 8pt; font-weight: 700; color: #c1121f; }
        """;

    private const int FilasTalonario = 22;

    public static string Generar(Factura f, Ajustes ajustes, bool reimpresion)
    {
        var n = ajustes.Negocio;
        var cuotaFija = !ajustes.Facturacion.IvaHabilitado;
        var c = new StringBuilder("<div class=\"hoja\">");
        if (f.Estado == EstadoFactura.Anulada) c.Append("<div class=\"sello\">ANULADA</div>");

        var marcas = new StringBuilder();
        if (reimpresion) marcas.Append("<span class=\"marca\">REIMPRESIÓN</span>");
        if (f.FechaModificacion is { } modificada) marcas.Append($"<span class=\"marca roja\">MODIFICADA {modificada:dd/MM/yyyy}</span>");
        if (f.Estado == EstadoFactura.Anulada) marcas.Append("<span class=\"marca roja\">ANULADA</span>");

        c.Append($"""
            <header class="cabecera">
              <div class="doc">
                <div class="tipo">FACTURA{(cuotaFija ? " CF" : "")}</div>
                <div class="sub">{E(ajustes.Facturacion.TipoFactura)}</div>
                <div class="sub">SERIE "{E(f.Serie)}"</div>
                <div class="numero">{f.Numero}</div>
                <table class="fecha">
                  <tr><th>DIA</th><th>MES</th><th>AÑO</th></tr>
                  <tr><td>{f.Fecha:dd}</td><td>{f.Fecha:MM}</td><td>{f.Fecha:yyyy}</td></tr>
                </table>
              </div>
              <div class="negocio">
                <div class="giro">{E(n.Giro)}</div>
                <div class="nombre">{E(n.NombreComercial)}</div>
                <div class="propietario">{E(n.Propietario)}</div>
                <div class="dato">{E(n.Direccion)}</div>
                {(n.Email.Length > 0 ? $"<div class=\"dato\">E-mail: {E(n.Email)}</div>" : "")}
                {(n.Telefono.Length > 0 ? $"<div class=\"dato\">Tel.: {E(Identificacion.FormatoTelefono(n.Telefono))}</div>" : "")}
                {(n.Ruc.Length > 0 ? $"<div class=\"dato\">RUC: {E(n.Ruc)}</div>" : "")}
              </div>
              <div class="marcas">{marcas}</div>
            </header>
            <div class="linea-dato"><span class="etq">CLIENTE:</span><span class="valor">{E(PlantillaPreimpreso.TextoCliente(f))}</span></div>
            <div class="linea-dato"><span class="etq">DIRECCIÓN:</span><span class="valor">{E(f.ClienteDireccion)}</span></div>
            <table class="lineas">
              <colgroup><col style="width:19.3mm"><col><col style="width:26.9mm"><col style="width:28.3mm"></colgroup>
              <thead><tr><th>CANT.</th><th>DESCRIPCIÓN</th><th>P. UNIT.</th><th>TOTAL</th></tr></thead>
              <tbody>
            """);

        foreach (var l in f.Lineas)
        {
            c.Append($"<tr><td class=\"cen\">{Dinero.Cantidad(l.Cantidad)}</td><td>{E(PlantillaPreimpreso.TextoDescripcion(l))}</td>" +
                     $"<td class=\"num\">{Dinero.Numero(l.PrecioUnitario)}</td><td class=\"num\">{Dinero.Numero(l.Total)}</td></tr>");
        }
        for (var i = f.Lineas.Count; i < FilasTalonario; i++)
            c.Append("<tr><td></td><td></td><td></td><td></td></tr>");

        c.Append($"""
              </tbody>
              <tfoot><tr><td></td><td class="etq-total">TOTAL</td><td></td><td class="num">{Dinero.Numero(f.Total)}</td></tr></tfoot>
            </table>
            <div class="pagare"><span></span><span>PAGARÉ A LA ORDEN</span><span class="cuota">{(cuotaFija ? "CUOTA FIJA" : "")}</span></div>
            """);

        if (n.Condiciones.Length > 0) c.Append($"<div class=\"condiciones\">{Multilinea(n.Condiciones)}</div>");
        c.Append("<div class=\"firmas\"><div>RECIBÍ CONFORME</div><div>ENTREGUÉ CONFORME</div></div>");

        if (f.Estado == EstadoFactura.Anulada)
            c.Append($"<div class=\"nota\">FACTURA ANULADA el {f.FechaAnulacion:dd/MM/yyyy HH:mm}. Motivo: {E(f.MotivoAnulacion)}</div>");
        if (f.Modificaciones.FirstOrDefault() is { } ultima)
            c.Append($"<div class=\"nota\">MODIFICADA el {ultima.Fecha:dd/MM/yyyy HH:mm} por {E(ultima.Empleado)}. Motivo: {E(ultima.Motivo)}</div>");

        c.Append("</div>");
        return Documento($"Factura {f.NumeroCompleto}", Estilos, c.ToString());
    }
}
