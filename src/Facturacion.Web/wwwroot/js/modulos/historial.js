// Historial de facturas: consulta del día (o rango), reimpresión y anulación.
import { api, conParametros } from '../api.js';
import { $, h, dinero, numero2, cantidad as formatoCantidad, debounce, aviso, abrirModal, pedirTexto, fechaHora, hora, hoyIso } from '../ui.js';
import { obtenerConfiguracion } from '../config.js';
import { imprimir, guardarPdf, rutaFactura, vistaPrevia, esEscritorio } from '../impresion.js';
import { actualizarAlertas } from '../app.js';

const FORMATOS = { Carta: 'Hoja carta', Ticket: 'Ticket', Preimpreso: 'Preimpreso' };

let raiz;
let facturas = [];
let filtro;
let formatoPredeterminado = 'Carta';

export async function montar(contenedor) {
  raiz = contenedor;
  filtro = { desde: hoyIso(), hasta: hoyIso(), texto: '', estado: '' };
  formatoPredeterminado = (await obtenerConfiguracion()).impresion.formato;

  raiz.innerHTML = h`
    <div class="encabezado">
      <div><h1>Historial de facturas</h1><p>Por defecto muestra las de hoy. Un número de factura se busca en cualquier fecha.</p></div>
    </div>
    <div class="tarjeta">
      <div class="barra">
        <div class="campo"><label>Desde</label><input type="date" id="desde" value="${filtro.desde}"></div>
        <div class="campo"><label>Hasta</label><input type="date" id="hasta" value="${filtro.hasta}"></div>
        <div class="campo crece"><label>Buscar <kbd>F1</kbd></label><input id="texto" placeholder="No. de factura, cliente o RUC" autocomplete="off"></div>
        <div class="campo"><label>Estado</label>
          <select id="estado"><option value="">Todas</option><option value="Emitida">Emitidas</option><option value="Anulada">Anuladas</option></select></div>
        <button type="button" class="btn" id="hoy">Hoy</button>
        <button type="button" class="btn" id="ayer">Ayer</button>
      </div>
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead><tr><th>No.</th><th>Fecha</th><th>Cliente</th><th>Pago</th><th class="num">Total</th><th>Estado</th><th></th></tr></thead>
          <tbody id="cuerpo"></tbody>
        </table>
        <div class="vacio" id="vacio" hidden>No hay facturas para este criterio.</div>
      </div>
      <div class="pie-tabla"><span id="resumen"></span></div>
    </div>`;

  const recargar = debounce(cargar, 300);
  $('#desde', raiz).addEventListener('change', (e) => { filtro.desde = e.target.value; cargar(); });
  $('#hasta', raiz).addEventListener('change', (e) => { filtro.hasta = e.target.value; cargar(); });
  $('#texto', raiz).addEventListener('input', (e) => { filtro.texto = e.target.value; recargar(); });
  $('#estado', raiz).addEventListener('change', (e) => { filtro.estado = e.target.value; cargar(); });
  $('#hoy', raiz).addEventListener('click', () => fijarDia(hoyIso()));
  $('#ayer', raiz).addEventListener('click', () => fijarDia(hoyIso(-1)));
  $('#cuerpo', raiz).addEventListener('click', (e) => {
    const fila = e.target.closest('tr[data-id]');
    if (!fila) return;
    const factura = facturas.find((f) => f.id === Number(fila.dataset.id));
    const accion = e.target.closest('[data-accion]')?.dataset.accion;
    if (accion === 'imprimir') reimprimir(factura.id, formatoPredeterminado);
    else if (accion === 'anular') anular(factura);
    else verDetalle(factura.id);
  });

  await cargar();
  return {
    teclas(tecla) {
      if (tecla === 'F1') { $('#texto', raiz).focus(); return true; }
      return false;
    }
  };
}

function fijarDia(dia) {
  filtro.desde = filtro.hasta = dia;
  $('#desde', raiz).value = dia;
  $('#hasta', raiz).value = dia;
  cargar();
}

async function cargar() {
  try {
    facturas = await api.get(conParametros('/api/facturas', filtro));
    if (!raiz.isConnected) return;
    $('#cuerpo', raiz).innerHTML = h`${facturas.map((f) => {
      const anulada = f.estado === 'Anulada';
      return h`
        <tr data-id="${f.id}" class="${anulada ? 'anulada' : ''}" style="cursor:pointer">
          <td><b>${f.numeroCompleto}</b></td>
          <td>${fechaHora(f.fecha)}</td>
          <td>${f.clienteNombre}</td>
          <td>${f.metodoPago}</td>
          <td class="num total-celda">${dinero(f.total)}</td>
          <td><span class="chip ${anulada ? 'chip-rojo' : 'chip-verde'}">${f.estado}</span></td>
          <td><div class="acciones-fila">
            <button type="button" class="btn btn-sm" data-accion="imprimir">Reimprimir</button>
            ${anulada ? '' : h`<button type="button" class="btn btn-sm" data-accion="anular">Anular</button>`}
          </div></td>
        </tr>`;
    })}`;
    $('#vacio', raiz).hidden = facturas.length > 0;
    const emitidas = facturas.filter((f) => f.estado === 'Emitida');
    const total = emitidas.reduce((s, f) => s + f.total, 0);
    const anuladas = facturas.length - emitidas.length;
    $('#resumen', raiz).textContent =
      `${emitidas.length} emitidas · ${anuladas} anuladas · Total emitido: ${dinero(total)}`;
  } catch (error) {
    aviso(error.message, 'error');
  }
}

async function reimprimir(id, formato) {
  try {
    await imprimir(rutaFactura(id, formato, true));
    aviso('Factura enviada a la impresora.');
  } catch (error) {
    aviso(error.message, 'error', 7000);
  }
}

async function verDetalle(id) {
  let f;
  try {
    f = await api.get(`/api/facturas/${id}`);
  } catch (error) {
    aviso(error.message, 'error');
    return;
  }
  const anulada = f.estado === 'Anulada';
  const pago = f.metodoPago === 'Efectivo'
    ? `Efectivo · recibido ${dinero(f.montoRecibido ?? f.total)} · vuelto ${dinero(f.vuelto ?? 0)}`
    : `${f.metodoPago}${f.referencia ? ` · ref. ${f.referencia}` : ''}`;

  const modal = abrirModal({
    titulo: `Factura ${f.numeroCompleto}`,
    ancho: 'lg',
    cuerpo: h`
      ${anulada ? h`<div class="aviso-caja" style="margin-bottom:10px">ANULADA el ${fechaHora(f.fechaAnulacion)} · Motivo: ${f.motivoAnulacion}</div>` : ''}
      <dl class="lista-datos">
        <dt>Fecha</dt><dd>${fechaHora(f.fecha)}</dd>
        <dt>Cliente</dt><dd>${f.clienteNombre}${f.clienteRuc ? h` · RUC ${f.clienteRuc}` : ''}</dd>
        ${f.clienteDireccion ? h`<dt>Dirección</dt><dd>${f.clienteDireccion}</dd>` : ''}
        <dt>Pago</dt><dd>${pago}</dd>
      </dl>
      <table class="tabla" style="margin-top:12px">
        <thead><tr><th class="num">Cant.</th><th>Descripción</th><th class="num">P. Unit.</th><th class="num">Total</th></tr></thead>
        <tbody>${f.lineas.map((l) => h`
          <tr><td class="num">${formatoCantidad(l.cantidad)} ${l.unidad}</td><td>${l.codigo ? h`<span class="mono suave">${l.codigo}</span> ` : ''}${l.descripcion}</td>
          <td class="num">${numero2(l.precioUnitario)}</td><td class="num">${numero2(l.total)}</td></tr>`)}</tbody>
      </table>
      <div style="text-align:right;margin-top:10px">
        ${f.iva ? h`<div>Subtotal ${dinero(f.subtotal)} · IVA ${dinero(f.iva)}</div>` : ''}
        <div style="font-size:18px"><b>Total ${dinero(f.total)}</b></div>
      </div>
      <div class="acciones" style="margin-top:14px">
        <label class="suave" for="formatoDetalle">Formato:</label>
        <select id="formatoDetalle" style="width:auto">
          ${Object.entries(FORMATOS).map(([valor, texto]) => h`<option value="${valor}" ${valor === formatoPredeterminado ? h`selected` : ''}>${texto}</option>`)}
        </select>
        <button type="button" class="btn" data-accion="vista">Vista previa</button>
        <button type="button" class="btn" data-accion="imprimir">Reimprimir</button>
        ${esEscritorio ? h`<button type="button" class="btn" data-accion="pdf">Guardar PDF</button>` : ''}
      </div>`,
    botones: [
      ...(anulada ? [] : [{ texto: 'Anular factura', clase: 'btn-peligro izquierda', izquierda: true, accion: () => { anular(f); } }]),
      { texto: 'Cerrar', primario: true }
    ]
  });

  modal.cuerpo.addEventListener('click', async (e) => {
    const accion = e.target.closest('[data-accion]')?.dataset.accion;
    if (!accion) return;
    const formato = $('#formatoDetalle', modal.cuerpo).value;
    if (accion === 'vista') vistaPrevia(rutaFactura(f.id, formato, true), `Factura ${f.numeroCompleto}`);
    else if (accion === 'imprimir') reimprimir(f.id, formato);
    else if (accion === 'pdf') {
      try { await guardarPdf(rutaFactura(f.id, 'Carta'), `Factura-${f.numeroCompleto}.pdf`); } catch (error) { aviso(error.message, 'error'); }
    }
  });
}

async function anular(factura) {
  const motivo = await pedirTexto({
    titulo: `Anular factura ${factura.numeroCompleto}`,
    mensaje: `Total ${dinero(factura.total)} · ${factura.clienteNombre}. La factura queda registrada como ANULADA y los productos regresan al inventario. Recuerde marcar también el formulario físico.`,
    etiqueta: 'Motivo de la anulación',
    textoSi: 'Anular',
    peligro: true
  });
  if (motivo === null) return;
  try {
    await api.post(`/api/facturas/${factura.id}/anular`, { motivo });
    aviso(`Factura ${factura.numeroCompleto} anulada.`);
    await cargar();
    actualizarAlertas();
  } catch (error) {
    aviso(error.message, 'error', 7000);
  }
}
