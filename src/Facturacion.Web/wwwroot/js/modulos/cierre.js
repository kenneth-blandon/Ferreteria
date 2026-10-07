// Cierre de caja: resumen del día por forma de pago.
import { api, conParametros } from '../api.js';
import { $, h, dinero, cantidad as formatoCantidad, aviso, hora, hoyIso, fecha } from '../ui.js';
import { imprimir, vistaPrevia } from '../impresion.js';

let raiz;
let dia;

export async function montar(contenedor) {
  raiz = contenedor;
  dia = hoyIso();
  raiz.innerHTML = h`
    <div class="encabezado">
      <div><h1>Cierre de caja</h1><p>Total vendido del día por forma de pago. Las facturas anuladas no suman.</p></div>
      <div class="acciones">
        <input type="date" id="dia" value="${dia}" style="width:auto">
        <button type="button" class="btn" id="vista">Vista previa</button>
        <button type="button" class="btn" id="ticket">Imprimir ticket</button>
        <button type="button" class="btn btn-primario" id="carta">Imprimir hoja carta</button>
      </div>
    </div>
    <div id="contenido"></div>`;

  $('#dia', raiz).addEventListener('change', (e) => { dia = e.target.value || hoyIso(); cargar(); });
  $('#vista', raiz).addEventListener('click', () => vistaPrevia(ruta('Carta'), `Cierre de caja ${fecha(dia + 'T00:00')}`));
  $('#ticket', raiz).addEventListener('click', () => imprimirCierre('Ticket'));
  $('#carta', raiz).addEventListener('click', () => imprimirCierre('Carta'));
  await cargar();
}

const ruta = (formato) => conParametros('/imprimir/cierre', { fecha: dia, formato });

async function imprimirCierre(formato) {
  try {
    await imprimir(ruta(formato));
    aviso('Cierre enviado a la impresora.');
  } catch (error) {
    aviso(error.message, 'error', 7000);
  }
}

async function cargar() {
  let r;
  try {
    r = await api.get(conParametros('/api/reportes/cierre', { fecha: dia }));
  } catch (error) {
    aviso(error.message, 'error');
    return;
  }
  if (!raiz.isConnected) return;

  const numeracion = r.primerNumero ? (r.primerNumero === r.ultimoNumero ? `${r.primerNumero}` : `${r.primerNumero} – ${r.ultimoNumero}`) : '—';
  $('#contenido', raiz).innerHTML = h`
    <div class="indicadores">
      <div class="tarjeta indicador"><span>Total vendido</span><b>${dinero(r.totalVendido)}</b></div>
      <div class="tarjeta indicador"><span>Facturas emitidas</span><b>${r.facturasEmitidas}</b></div>
      <div class="tarjeta indicador"><span>Facturas anuladas</span><b class="${r.facturasAnuladas ? 'peligro-texto' : ''}">${r.facturasAnuladas}</b></div>
      <div class="tarjeta indicador"><span>Numeración usada</span><b>${numeracion}</b></div>
    </div>
    <div class="columnas">
      <div class="tarjeta seccion">
        <h2>Por forma de pago</h2>
        <table class="tabla">
          <thead><tr><th>Forma de pago</th><th class="num">Facturas</th><th class="num">Total</th></tr></thead>
          <tbody>${r.porMetodo.map((m) => h`<tr><td>${m.metodo}</td><td class="num">${m.facturas}</td><td class="num">${dinero(m.total)}</td></tr>`)}</tbody>
          <tfoot><tr><th>Total</th><th class="num">${r.facturasEmitidas}</th><th class="num">${dinero(r.totalVendido)}</th></tr></tfoot>
        </table>
        ${r.iva ? h`<p class="suave">Subtotal ${dinero(r.subtotal)} · IVA ${dinero(r.iva)}</p>` : ''}
        ${r.totalAnulado ? h`<p class="suave">Monto anulado (no suma): ${dinero(r.totalAnulado)}</p>` : ''}
      </div>
      <div class="tarjeta seccion">
        <h2>Productos más vendidos</h2>
        ${r.productosMasVendidos.length
          ? h`<table class="tabla"><thead><tr><th>Producto</th><th class="num">Cantidad</th><th class="num">Total</th></tr></thead>
              <tbody>${r.productosMasVendidos.map((p) => h`<tr><td>${p.descripcion}</td><td class="num">${formatoCantidad(p.cantidad)} ${p.unidad}</td><td class="num">${dinero(p.total)}</td></tr>`)}</tbody></table>`
          : h`<div class="vacio">Sin ventas este día.</div>`}
      </div>
    </div>
    <div class="tarjeta seccion" style="margin-top:12px">
      <h2>Facturas del día</h2>
      ${r.facturas.length
        ? h`<table class="tabla"><thead><tr><th>No.</th><th>Hora</th><th>Cliente</th><th>Pago</th><th class="num">Total</th><th>Estado</th></tr></thead>
            <tbody>${r.facturas.map((f) => h`<tr class="${f.estado === 'Anulada' ? 'anulada' : ''}">
              <td>${f.numeroCompleto}</td><td>${hora(f.fecha)}</td><td>${f.clienteNombre}</td><td>${f.metodoPago}</td>
              <td class="num total-celda">${dinero(f.total)}</td><td>${f.estado}</td></tr>`)}</tbody></table>`
        : h`<div class="vacio">No hay facturas en esta fecha.</div>`}
    </div>`;
}
