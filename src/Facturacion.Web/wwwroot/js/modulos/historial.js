// Historial de facturas: consulta del día (o rango), reimpresión, modificación y anulación.
import { api, conParametros } from '../api.js';
import { $, h, dinero, numero2, cantidad as formatoCantidad, debounce, aviso, abrirModal, pedirTexto, fechaHora, hoyIso } from '../ui.js';
import { imprimir, guardarPdf, rutaFactura, rutaVista, vistaPrevia, esEscritorio } from '../impresion.js';
import { actualizarAlertas } from '../app.js';

let raiz;
let facturas = [];
let filtro;

export async function montar(contenedor) {
  raiz = contenedor;
  filtro = { desde: hoyIso(), hasta: hoyIso(), texto: '', estado: '' };

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
    if (accion === 'imprimir') reimprimir(factura);
    else if (accion === 'modificar') modificar(factura);
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

function chipsEstado(f) {
  const anulada = f.estado === 'Anulada';
  return h`<span class="chip ${anulada ? 'chip-rojo' : 'chip-verde'}">${f.estado}</span>
    ${f.modificada && !anulada ? h`<span class="chip chip-ambar" title="Modificada el ${fechaHora(f.fechaModificacion)}">MODIFICADA</span>` : ''}`;
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
          <td><div class="chips">${chipsEstado(f)}</div></td>
          <td><div class="acciones-fila">
            <button type="button" class="btn btn-sm" data-accion="imprimir" title="Imprimir en el talonario">Imprimir</button>
            ${anulada ? '' : h`
              <button type="button" class="btn btn-sm" data-accion="modificar">Modificar</button>
              <button type="button" class="btn btn-sm" data-accion="anular">Anular</button>`}
          </div></td>
        </tr>`;
    })}`;
    $('#vacio', raiz).hidden = facturas.length > 0;
    const emitidas = facturas.filter((f) => f.estado === 'Emitida');
    const total = emitidas.reduce((s, f) => s + f.total, 0);
    const modificadas = emitidas.filter((f) => f.modificada).length;
    $('#resumen', raiz).textContent =
      `${emitidas.length} emitidas (${modificadas} modificadas) · ${facturas.length - emitidas.length} anuladas · Total emitido: ${dinero(total)}`;
  } catch (error) {
    aviso(error.message, 'error');
  }
}

async function reimprimir(factura) {
  try {
    await imprimir(rutaFactura(factura.id));
    aviso(`Factura ${factura.numeroCompleto} enviada a la impresora (formulario No. ${factura.numero}).`);
  } catch (error) {
    aviso(error.message, 'error', 7000);
  }
}

/** Lleva la factura a la pantalla de facturación para corregirla. */
function modificar(factura) {
  location.hash = `#/facturar?modificar=${factura.id}`;
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
        <dt>Estado</dt><dd><div class="chips">${chipsEstado(f)}</div></dd>
        <dt>Cliente</dt><dd>${f.clienteNombre}${f.clienteRuc ? h` · RUC/Céd. ${f.clienteRuc}` : ''}</dd>
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
      ${f.modificaciones.length ? h`
        <h3 style="margin:14px 0 6px">Modificaciones</h3>
        <div class="modificaciones">${f.modificaciones.map((m) => h`
          <div class="modificacion">
            <div class="modificacion-cabecera"><b>${fechaHora(m.fecha)}</b> · ${m.empleado}
              <span class="suave">· total ${dinero(m.totalAnterior)} → ${dinero(m.totalNuevo)}</span></div>
            <div><b>Motivo:</b> ${m.motivo}</div>
            <ul class="lista-cambios">${m.cambios.map((c) => h`<li>${c}</li>`)}</ul>
          </div>`)}</div>` : ''}
      <div class="acciones" style="margin-top:14px">
        <button type="button" class="btn" data-accion="vista">Vista previa</button>
        <button type="button" class="btn" data-accion="imprimir">Imprimir en talonario</button>
        ${esEscritorio ? h`<button type="button" class="btn" data-accion="pdf">Guardar PDF</button>` : ''}
      </div>`,
    botones: [
      ...(anulada ? [] : [
        { texto: 'Anular', clase: 'btn-peligro izquierda', izquierda: true, accion: () => { anular(f); } },
        { texto: 'Modificar', izquierda: true, accion: () => { modificar(f); } }
      ]),
      { texto: 'Cerrar', primario: true }
    ]
  });

  modal.cuerpo.addEventListener('click', async (e) => {
    const accion = e.target.closest('[data-accion]')?.dataset.accion;
    if (!accion) return;
    if (accion === 'vista') {
      vistaPrevia(rutaVista(f.id, true), `Vista previa · factura ${f.numeroCompleto}`,
        { rutaImpresion: rutaFactura(f.id), textoImprimir: 'Imprimir en talonario' });
    } else if (accion === 'imprimir') {
      reimprimir(f);
    } else if (accion === 'pdf') {
      try { await guardarPdf(rutaVista(f.id), `Factura-${f.numeroCompleto}.pdf`); } catch (error) { aviso(error.message, 'error'); }
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
