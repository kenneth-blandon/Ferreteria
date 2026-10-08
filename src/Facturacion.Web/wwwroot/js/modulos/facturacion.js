// Pantalla de facturación rápida (mostrador).
// Flujo con teclado: F1 buscar → Enter agrega → escribir cantidad → Enter vuelve al buscador → F10 generar.
// Después de generar, se indica qué formulario del talonario colocar en la impresora y se imprime.
// También sirve para MODIFICAR una factura emitida (desde Historial): se cargan sus datos y al guardar
// se piden el motivo y el nombre del empleado.
import { api, conParametros } from '../api.js';
import {
  $, $$, h, dinero, numero2, cantidad as formatoCantidad, valorEntrada, aNumero, redondear, debounce,
  aviso, confirmar, abrirModal, leerLocal, guardarLocal
} from '../ui.js';
import { obtenerConfiguracion } from '../config.js';
import { guardarPdf, rutaFactura, rutaVista, vistaPrevia, dialogoImprimir, imprimir, esEscritorio } from '../impresion.js';
import { documento, validarAlSalir } from '../validacion.js';
import { actualizarAlertas } from '../app.js';

const CLAVE_ESTADO = 'facturacion.facturaEnCurso';
const CLAVE_MODIFICACION = 'facturacion.modificacionEnCurso';
const CLAVE_ULTIMA = 'facturacion.ultimaFactura';
const CONSUMIDOR_FINAL = 'CONSUMIDOR FINAL';
const METODOS = ['Efectivo', 'Tarjeta', 'Transferencia'];

let raiz;
let config;
let estado;
let ultima;
let unidades = [];
let proximo = null;
let resultados = [];
let textoResultados = '';
let indice = -1;
let turnoBusqueda = 0;
let clientes = [];
let indiceCliente = -1;
let cobrando = false;
let revisarRuc = () => true;
// Búsquedas por Enter que aún esperan respuesta: cobrar espera a que terminen.
const pendientes = new Set();

const modificando = () => estado.modo === 'modificar';
const maximoLineas = () => config.impresion.plantilla.filas.maximo;

// ---------- Montaje ----------

export async function montar(contenedor, parametros) {
  raiz = contenedor;
  config = await obtenerConfiguracion(true);
  ultima = leerLocal(CLAVE_ULTIMA);
  unidades = await api.get('/api/unidades').then((lista) => lista.map((u) => u.nombre)).catch(() => ['UND']);

  const idModificar = Number(parametros.get('modificar'));
  if (idModificar) {
    // La dirección vuelve a #/facturar: si se recarga la página se sigue con la modificación guardada.
    history.replaceState(null, '', '#/facturar');
    const enCurso = leerLocal(CLAVE_MODIFICACION);
    if (enCurso?.facturaId !== idModificar) {
      try {
        guardarLocal(CLAVE_MODIFICACION, await cargarParaModificar(idModificar));
      } catch (error) {
        aviso(error.message, 'error', 7000);
      }
    }
  }
  estado = cargarEstado();

  raiz.innerHTML = plantilla();
  enlazarEventos();
  pintarTodo();
  actualizarNumero();
  enfocar('#busqueda');
  return { teclas };
}

function teclas(tecla) {
  switch (tecla) {
    case 'F1': enfocar('#busqueda'); return true;
    case 'F2': enfocar('#cliNombre'); return true;
    case 'F4': articuloLibre(); return true;
    case 'F8': cambiarMetodo('Efectivo'); enfocar('#recibido'); return true;
    case 'F9':
    case 'F10':
    case 'Ctrl+Enter': cobrar(); return true;
    case 'Escape': limpiar(); return true;
    default: return false;
  }
}

function plantilla() {
  return h`
  <section class="pos">
    <div class="pos-izq">
      <div class="pos-titulo">
        <div>
          <h1><span id="tituloFactura">Factura</span> <span id="numFactura" class="chip chip-azul">…</span></h1>
          <div id="avisoNumeracion" class="aviso-texto" hidden></div>
        </div>
        <div class="acciones">
          <button type="button" class="btn" id="btnLibre" title="Artículo que no está en el catálogo">+ Artículo libre <kbd>F4</kbd></button>
          <button type="button" class="btn" id="btnLimpiar">Limpiar <kbd>Esc</kbd></button>
        </div>
      </div>

      <div class="aviso-caja banda-modificacion" id="bandaModificacion" hidden></div>

      <div class="tarjeta pos-cliente">
        <div class="campo crece">
          <label for="cliNombre">Cliente <kbd>F2</kbd></label>
          <div class="autocompletar">
            <input id="cliNombre" autocomplete="off" spellcheck="false" placeholder="${CONSUMIDOR_FINAL}">
            <div class="sugerencias" id="cliSugerencias" hidden></div>
          </div>
        </div>
        <div class="campo"><label for="cliRuc">RUC / Cédula</label>
          <input id="cliRuc" autocomplete="off" spellcheck="false" placeholder="000-000000-0000A"></div>
        <div class="campo crece"><label for="cliDireccion">Dirección</label><input id="cliDireccion" autocomplete="off"></div>
        <div class="campo-fin">
          <label class="check" id="cajaGuardar" hidden><input type="checkbox" id="cliGuardar"> Guardar cliente</label>
          <button type="button" class="btn-link" id="btnConsumidor">Consumidor final</button>
        </div>
      </div>

      <div class="tarjeta pos-buscar">
        <div class="autocompletar">
          <input id="busqueda" class="input-grande" autocomplete="off" spellcheck="false"
                 placeholder="Buscar producto por código, nombre o código de barras  (F1)">
          <div class="sugerencias grande" id="resultados" hidden></div>
        </div>
        <div class="ayuda">Enter agrega · ↑ ↓ elegir · <b>5*código</b> agrega 5 unidades · un producto repetido suma cantidad</div>
      </div>

      <div class="tarjeta pos-lineas">
        <table class="tabla tabla-lineas">
          <thead><tr>
            <th class="col-n">#</th><th>Código</th><th>Descripción</th><th class="num col-cant">Cantidad</th>
            <th class="col-unidad">Unidad</th><th class="num col-precio">P. Unit.</th><th class="num col-total">Total</th><th class="col-acc"></th>
          </tr></thead>
          <tbody id="lineas"></tbody>
        </table>
        <div class="vacio" id="sinLineas">Busque un producto para comenzar la factura.</div>
      </div>
    </div>

    <aside class="tarjeta pos-der">
      <div class="totales">
        <div class="fila-total" id="filaSubtotal"><span>Subtotal</span><b id="tSubtotal">C$ 0.00</b></div>
        <div class="fila-total" id="filaIva"><span id="etqIva">IVA</span><b id="tIva">C$ 0.00</b></div>
        <div class="gran-total"><span>TOTAL</span><b id="tTotal">C$ 0.00</b></div>
        <div class="sub-info" id="infoLineas">Sin productos</div>
      </div>

      <div class="bloque">
        <label>Forma de pago</label>
        <div class="segmentado" id="metodos">
          ${METODOS.map((m) => h`<button type="button" data-metodo="${m}">${m}</button>`)}
        </div>
      </div>

      <div class="bloque" id="bloqueEfectivo">
        <label for="recibido">Efectivo recibido <kbd>F8</kbd></label>
        <input id="recibido" class="input-grande num" inputmode="decimal" autocomplete="off" placeholder="Exacto">
        <div class="billetes" id="billetes"></div>
        <div class="vuelto neutral" id="vuelto"><span>Vuelto</span><b>C$ 0.00</b></div>
      </div>

      <div class="bloque" id="bloqueReferencia" hidden>
        <label for="referencia">Referencia / No. de operación</label>
        <input id="referencia" autocomplete="off" spellcheck="false">
      </div>

      <button type="button" class="btn btn-exito btn-cobrar" id="btnCobrar"></button>
      <div class="sub-info-claro" id="infoTalonario"></div>
      <div class="ultima" id="ultima" hidden></div>
    </aside>
  </section>`;
}

// ---------- Estado (se guarda localmente: no se pierde si se cierra la ventana) ----------

function estadoVacio() {
  return {
    modo: 'nueva',
    cliente: { id: null, nombre: '', ruc: '', direccion: '', guardar: false },
    lineas: [],
    metodoPago: 'Efectivo',
    recibido: '',
    referencia: ''
  };
}

function normalizarEstado(guardado) {
  const base = estadoVacio();
  if (!guardado || !Array.isArray(guardado.lineas)) return base;
  return {
    ...base,
    ...guardado,
    recibido: String(guardado.recibido ?? ''),
    referencia: String(guardado.referencia ?? ''),
    cliente: { ...base.cliente, ...guardado.cliente },
    metodoPago: METODOS.includes(guardado.metodoPago) ? guardado.metodoPago : 'Efectivo'
  };
}

/** Una modificación en curso tiene prioridad sobre la factura nueva que se estaba armando. */
function cargarEstado() {
  const modificacion = leerLocal(CLAVE_MODIFICACION);
  if (modificacion?.modo === 'modificar') return normalizarEstado(modificacion);
  return normalizarEstado(leerLocal(CLAVE_ESTADO));
}

const guardarEstado = () => guardarLocal(modificando() ? CLAVE_MODIFICACION : CLAVE_ESTADO, estado);
const enfocar = (selector) => {
  const elemento = $(selector, raiz);
  elemento?.focus();
  elemento?.select?.();
};

/** Arma el estado de edición a partir de una factura emitida. */
async function cargarParaModificar(id) {
  const f = await api.get(`/api/facturas/${id}`);
  if (f.estado === 'Anulada') throw new Error(`La factura ${f.numeroCompleto} está anulada y no se puede modificar.`);

  // Lo ya facturado vuelve al inventario al corregir: cuenta como disponible.
  const facturado = {};
  for (const l of f.lineas) if (l.productoId) facturado[l.productoId] = (facturado[l.productoId] || 0) + l.cantidad;
  const existencias = {};
  await Promise.all(Object.keys(facturado).map(async (pid) => {
    existencias[pid] = await api.get(`/api/productos/${pid}`).then((p) => p.stock).catch(() => 0);
  }));

  return {
    modo: 'modificar',
    facturaId: f.id,
    numero: f.numero,
    numeroCompleto: f.numeroCompleto,
    facturado,
    cliente: {
      id: f.clienteId,
      nombre: f.clienteNombre === CONSUMIDOR_FINAL ? '' : f.clienteNombre,
      ruc: f.clienteRuc || '',
      direccion: f.clienteDireccion || '',
      guardar: false
    },
    lineas: f.lineas.map((l) => ({
      productoId: l.productoId,
      codigo: l.codigo,
      descripcion: l.descripcion,
      unidad: l.unidad,
      cantidad: l.cantidad,
      precio: l.precioUnitario,
      stock: l.productoId ? (existencias[l.productoId] ?? 0) + facturado[l.productoId] : null
    })),
    metodoPago: f.metodoPago,
    recibido: f.metodoPago === 'Efectivo' && f.montoRecibido ? valorEntrada(f.montoRecibido) : '',
    referencia: f.referencia || ''
  };
}

// ---------- Eventos ----------

function enlazarEventos() {
  const busqueda = $('#busqueda', raiz);
  busqueda.addEventListener('input', () => buscar());
  busqueda.addEventListener('keydown', teclaBusqueda);
  busqueda.addEventListener('blur', () => setTimeout(cerrarResultados, 150));

  $('#resultados', raiz).addEventListener('mousedown', (e) => {
    const item = e.target.closest('[data-i]');
    if (!item) return;
    e.preventDefault();
    const { cantidad } = separarCantidad(busqueda.value);
    agregarProducto(resultados[Number(item.dataset.i)], cantidad, true);
  });

  const tabla = $('#lineas', raiz);
  tabla.addEventListener('input', (e) => editarCelda(e.target, false));
  tabla.addEventListener('change', (e) => editarCelda(e.target, true));
  tabla.addEventListener('focusin', (e) => { if (e.target.matches('input')) e.target.select(); });
  tabla.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && e.target.matches('input')) {
      e.preventDefault();
      editarCelda(e.target, true);
      enfocar('#busqueda');
    }
  });
  tabla.addEventListener('click', (e) => {
    const boton = e.target.closest('[data-quitar]');
    if (!boton) return;
    estado.lineas.splice(Number(boton.closest('tr').dataset.i), 1);
    guardarEstado();
    pintarLineas();
    pintarTotales();
    enfocar('#busqueda');
  });

  // Cliente
  const nombre = $('#cliNombre', raiz);
  nombre.addEventListener('input', () => {
    estado.cliente.nombre = nombre.value;
    estado.cliente.id = null;
    guardarEstado();
    pintarGuardarCliente();
    buscarClientes();
  });
  nombre.addEventListener('keydown', teclaCliente);
  nombre.addEventListener('blur', () => setTimeout(cerrarClientes, 150));
  $('#cliSugerencias', raiz).addEventListener('mousedown', (e) => {
    const item = e.target.closest('[data-i]');
    if (!item) return;
    e.preventDefault();
    seleccionarCliente(clientes[Number(item.dataset.i)]);
  });
  $('#cliRuc', raiz).addEventListener('input', (e) => { estado.cliente.ruc = e.target.value; guardarEstado(); });
  revisarRuc = validarAlSalir($('#cliRuc', raiz), documento);
  $('#cliDireccion', raiz).addEventListener('input', (e) => { estado.cliente.direccion = e.target.value; guardarEstado(); });
  $('#cliGuardar', raiz).addEventListener('change', (e) => { estado.cliente.guardar = e.target.checked; guardarEstado(); });
  for (const id of ['#cliRuc', '#cliDireccion']) {
    $(id, raiz).addEventListener('keydown', (e) => {
      if (e.key === 'Enter') { e.preventDefault(); enfocar('#busqueda'); }
    });
  }
  $('#btnConsumidor', raiz).addEventListener('click', () => {
    estado.cliente = { id: null, nombre: '', ruc: '', direccion: '', guardar: false };
    guardarEstado();
    pintarCliente();
    enfocar('#busqueda');
  });

  // Pago
  $('#metodos', raiz).addEventListener('click', (e) => {
    const boton = e.target.closest('[data-metodo]');
    if (boton) cambiarMetodo(boton.dataset.metodo);
  });
  const recibido = $('#recibido', raiz);
  recibido.addEventListener('input', () => { estado.recibido = recibido.value; guardarEstado(); pintarVuelto(); });
  recibido.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); cobrar(); } });
  $('#billetes', raiz).addEventListener('click', (e) => {
    const boton = e.target.closest('[data-monto]');
    if (!boton) return;
    estado.recibido = boton.dataset.monto;
    recibido.value = estado.recibido;
    guardarEstado();
    pintarVuelto();
  });
  $('#referencia', raiz).addEventListener('input', (e) => { estado.referencia = e.target.value; guardarEstado(); });

  $('#btnCobrar', raiz).addEventListener('click', cobrar);
  $('#btnLimpiar', raiz).addEventListener('click', limpiar);
  $('#btnLibre', raiz).addEventListener('click', articuloLibre);
  $('#ultima', raiz).addEventListener('click', accionUltima);
  $('#bandaModificacion', raiz).addEventListener('click', (e) => {
    if (e.target.closest('[data-cancelar]')) limpiar();
  });
}

// ---------- Búsqueda de productos ----------

/** "5*tubo" → { cantidad: 5, texto: "tubo" }. */
function separarCantidad(valor) {
  const coincidencia = /^\s*(\d+(?:[.,]\d+)?)\s*\*\s*(.*)$/.exec(valor);
  if (coincidencia) return { cantidad: aNumero(coincidencia[1]), texto: coincidencia[2].trim() };
  return { cantidad: 1, texto: valor.trim() };
}

const buscar = debounce(async () => {
  if (!raiz?.isConnected) return;
  const { texto } = separarCantidad($('#busqueda', raiz).value);
  if (texto.length < 2) { cerrarResultados(); return; }
  await consultarProductos(texto);
}, 140);

async function consultarProductos(texto) {
  const miTurno = ++turnoBusqueda;
  try {
    const lista = await api.get(conParametros('/api/productos/buscar', { q: texto, limite: 20 }));
    if (miTurno !== turnoBusqueda || !raiz.isConnected) return null;
    resultados = lista;
    textoResultados = texto;
    indice = lista.length ? 0 : -1;
    pintarResultados(texto);
    return lista;
  } catch (error) {
    aviso(error.message, 'error');
    return null;
  }
}

function pintarResultados(texto) {
  const lista = $('#resultados', raiz);
  if (document.activeElement !== $('#busqueda', raiz)) { lista.hidden = true; return; }
  lista.innerHTML = resultados.length
    ? h`${resultados.map((p, i) => h`
        <div class="sugerencia ${i === indice ? 'activa' : ''}" data-i="${i}">
          <div class="sug-principal"><span class="mono">${p.codigo}</span><span>${p.nombre}</span></div>
          <div class="sug-detalle">
            <span>${p.categoria || 'Sin categoría'}</span>
            <span class="${claseStock(p)}">Stock: ${formatoCantidad(disponible(p))} ${p.unidad}</span>
            <b>${dinero(p.precio)}</b>
          </div>
        </div>`)}`
    : h`<div class="sugerencia vacia">Sin resultados para "${texto}". Use F4 para un artículo libre.</div>`;
  lista.hidden = false;
}

/** Existencia disponible; al modificar, lo ya facturado en esta factura también cuenta. */
const disponible = (producto) => producto.stock + (modificando() ? estado.facturado?.[producto.id] || 0 : 0);

function claseStock(p) {
  if (disponible(p) <= 0) return 'stock-agotado';
  return p.bajoStock ? 'stock-bajo' : '';
}

function marcarActivo() {
  $$('#resultados .sugerencia[data-i]', raiz).forEach((el, i) => {
    el.classList.toggle('activa', i === indice);
    if (i === indice) el.scrollIntoView({ block: 'nearest' });
  });
}

function cerrarResultados() {
  if (!raiz?.isConnected) return;
  $('#resultados', raiz).hidden = true;
}

function teclaBusqueda(e) {
  const abierta = !$('#resultados', raiz).hidden && resultados.length > 0;
  if (e.key === 'ArrowDown' && abierta) {
    e.preventDefault();
    indice = (indice + 1) % resultados.length;
    marcarActivo();
  } else if (e.key === 'ArrowUp' && abierta) {
    e.preventDefault();
    indice = (indice - 1 + resultados.length) % resultados.length;
    marcarActivo();
  } else if (e.key === 'Enter') {
    e.preventDefault();
    const tarea = confirmarBusqueda();
    pendientes.add(tarea);
    tarea.finally(() => pendientes.delete(tarea)).catch(() => {});
  } else if (e.key === 'Escape' && !$('#resultados', raiz).hidden) {
    e.preventDefault();
    e.stopPropagation();
    cerrarResultados();
  }
}

/**
 * Enter en el buscador:
 * 1) si la lista corresponde a lo escrito, agrega el producto marcado;
 * 2) si no, busca el código exacto (lector de código de barras);
 * 3) si tampoco, busca por nombre y agrega si hay un único resultado.
 */
async function confirmarBusqueda() {
  const entrada = $('#busqueda', raiz);
  const original = entrada.value;
  const { cantidad, texto } = separarCantidad(original);
  if (!texto) return;
  if (!(cantidad > 0)) { aviso('La cantidad indicada no es válida.', 'error'); return; }

  if (textoResultados === texto && indice >= 0 && resultados[indice]) {
    agregarProducto(resultados[indice], cantidad, true);
    return;
  }

  // Se vacía antes de consultar: si el lector de código de barras envía otro código
  // mientras tanto, ese texto nuevo no se mezcla ni se borra al agregar este.
  entrada.value = '';
  try {
    const producto = await api.get(conParametros('/api/productos/por-codigo', { codigo: texto }));
    agregarProducto(producto, cantidad, false, false);
    return;
  } catch (error) {
    if (!raiz.isConnected) return;
    if (entrada.value === '') entrada.value = original;
    if (error.estado !== 404) { aviso(error.message, 'error'); return; }
  }

  if (texto.length < 2 || entrada.value !== original) return;
  const lista = await consultarProductos(texto);
  if (lista?.length === 1) agregarProducto(lista[0], cantidad, true);
  else if (lista?.length === 0) aviso(`No se encontró "${texto}".`, 'aviso');
}

function limpiarBusqueda(vaciarCampo = true) {
  const entrada = $('#busqueda', raiz);
  if (vaciarCampo) entrada.value = '';
  resultados = [];
  textoResultados = '';
  indice = -1;
  cerrarResultados();
}

// ---------- Líneas ----------

const totalLinea = (l) => redondear((Number(l.cantidad) || 0) * (Number(l.precio) || 0));

function agregarProducto(producto, cantidad, enfocarCantidad, vaciarBusqueda = true) {
  if (!producto || !raiz.isConnected) return;
  let i = estado.lineas.findIndex((l) => l.productoId === producto.id);
  const nuevaCantidad = redondear((i >= 0 ? Number(estado.lineas[i].cantidad) : 0) + cantidad, 3);
  const existencia = disponible(producto);

  if (!config.facturacion.permitirStockNegativo && nuevaCantidad > existencia) {
    aviso(`Existencia insuficiente de ${producto.nombre}: hay ${formatoCantidad(existencia)} ${producto.unidad}.`, 'error', 6000);
    limpiarBusqueda(vaciarBusqueda);
    return;
  }
  if (i < 0 && estado.lineas.length >= maximoLineas()) {
    aviso(`El talonario tiene ${maximoLineas()} líneas. Genere esta factura y continúe en otra.`, 'error', 7000);
    limpiarBusqueda(vaciarBusqueda);
    return;
  }

  if (i >= 0) {
    estado.lineas[i].cantidad = nuevaCantidad;
    estado.lineas[i].stock = existencia;
  } else {
    estado.lineas.push({
      productoId: producto.id,
      codigo: producto.codigo,
      descripcion: producto.nombre,
      unidad: producto.unidad,
      cantidad: nuevaCantidad,
      precio: producto.precio,
      stock: existencia
    });
    i = estado.lineas.length - 1;
  }
  if (nuevaCantidad > existencia) {
    aviso(`Atención: el sistema registra ${formatoCantidad(existencia)} ${producto.unidad} de ${producto.nombre}.`, 'aviso', 5000);
  }

  limpiarBusqueda(vaciarBusqueda);
  guardarEstado();
  pintarLineas(i);
  pintarTotales();
  if (enfocarCantidad) enfocar(`#lineas tr[data-i="${i}"] input[data-campo="cantidad"]`);
  else $('#busqueda', raiz).focus();
}

function editarCelda(entrada, normalizar) {
  if (!entrada.matches('input[data-campo]')) return;
  const fila = entrada.closest('tr');
  const linea = estado.lineas[Number(fila.dataset.i)];
  if (!linea) return;

  const campo = entrada.dataset.campo;
  const valor = aNumero(entrada.value);
  const valido = campo === 'cantidad' ? valor > 0 : valor >= 0;
  entrada.classList.toggle('invalido', !valido);
  if (!valido) {
    if (normalizar) entrada.value = valorEntrada(linea[campo], campo === 'cantidad' ? 3 : 2);
    entrada.classList.remove('invalido');
    return;
  }

  linea[campo] = campo === 'cantidad' ? redondear(valor, 3) : redondear(valor);
  guardarEstado();
  $('.total-linea', fila).textContent = numero2(totalLinea(linea));
  pintarTotales();
  if (normalizar) {
    entrada.value = valorEntrada(linea[campo], campo === 'cantidad' ? 3 : 2);
    const etiqueta = $('.etiqueta-linea.alerta', fila);
    if (etiqueta) etiqueta.hidden = !(linea.productoId && linea.cantidad > linea.stock);
  }
}

function pintarLineas(resaltar = -1) {
  $('#lineas', raiz).innerHTML = h`${estado.lineas.map((l, i) => h`
    <tr data-i="${i}" class="${i === resaltar ? 'resaltada' : ''}">
      <td class="col-n">${i + 1}</td>
      <td class="mono">${l.codigo || '—'}</td>
      <td>
        <span class="desc-linea">${l.descripcion}</span>
        ${l.productoId
          ? h`<span class="etiqueta-linea alerta" ${l.cantidad > l.stock ? '' : h`hidden`}>Stock: ${formatoCantidad(l.stock)}</span>`
          : h`<span class="etiqueta-linea libre">Libre</span>`}
      </td>
      <td class="num"><input class="input-celda num" data-campo="cantidad" inputmode="decimal" value="${valorEntrada(l.cantidad, 3)}"></td>
      <td class="col-unidad">${l.unidad}</td>
      <td class="num"><input class="input-celda num" data-campo="precio" inputmode="decimal" value="${valorEntrada(l.precio)}"></td>
      <td class="num total-linea">${numero2(totalLinea(l))}</td>
      <td class="col-acc"><button type="button" class="btn-icono peligro" data-quitar title="Quitar línea">✕</button></td>
    </tr>`)}`;
  $('#sinLineas', raiz).hidden = estado.lineas.length > 0;
}

// ---------- Totales y pago ----------

function calcularTotales() {
  const suma = redondear(estado.lineas.reduce((s, l) => s + totalLinea(l), 0));
  const f = config.facturacion;
  if (!f.ivaHabilitado || f.ivaPorcentaje <= 0) return { subtotal: suma, iva: 0, total: suma };
  const tasa = f.ivaPorcentaje / 100;
  if (f.preciosIncluyenIva) {
    const subtotal = redondear(suma / (1 + tasa));
    return { subtotal, iva: redondear(suma - subtotal), total: suma };
  }
  const iva = redondear(suma * tasa);
  return { subtotal: suma, iva, total: redondear(suma + iva) };
}

function pintarTotales() {
  const { subtotal, iva, total } = calcularTotales();
  const conIva = config.facturacion.ivaHabilitado;
  $('#filaSubtotal', raiz).hidden = !conIva;
  $('#filaIva', raiz).hidden = !conIva;
  $('#etqIva', raiz).textContent = `IVA ${config.facturacion.ivaPorcentaje}%${config.facturacion.preciosIncluyenIva ? ' (incluido)' : ''}`;
  $('#tSubtotal', raiz).textContent = dinero(subtotal);
  $('#tIva', raiz).textContent = dinero(iva);
  $('#tTotal', raiz).textContent = dinero(total);

  const n = estado.lineas.length;
  const unidadesTotales = estado.lineas.reduce((s, l) => s + (Number(l.cantidad) || 0), 0);
  $('#infoLineas', raiz).textContent = n
    ? `${n} de ${maximoLineas()} líneas del talonario · ${formatoCantidad(unidadesTotales)} unidades`
    : 'Sin productos';

  $('#billetes', raiz).innerHTML = h`${sugerenciasEfectivo(total).map((monto, i) =>
    h`<button type="button" data-monto="${valorEntrada(monto)}">${i === 0 ? 'Exacto' : dinero(monto)}</button>`)}`;
  pintarVuelto();
}

/** Montos típicos con los que paga el cliente (billetes de 50, 100, 200, 500 y 1000). */
function sugerenciasEfectivo(total) {
  if (total <= 0) return [];
  const montos = new Set([total]);
  for (const billete of [50, 100, 200, 500, 1000]) {
    const monto = Math.ceil(total / billete) * billete;
    if (monto > total) montos.add(monto);
  }
  return [...montos].sort((a, b) => a - b).slice(0, 5);
}

function pintarVuelto() {
  const { total } = calcularTotales();
  const caja = $('#vuelto', raiz);
  const recibido = aNumero(estado.recibido);
  caja.classList.remove('falta', 'neutral');
  if (estado.recibido.trim() === '' || isNaN(recibido)) {
    caja.classList.add('neutral');
    caja.innerHTML = h`<span>Vuelto</span><b>${estado.recibido.trim() === '' ? '—' : 'Monto no válido'}</b>`;
  } else if (recibido < total) {
    caja.classList.add('falta');
    caja.innerHTML = h`<span>Faltan</span><b>${dinero(total - recibido)}</b>`;
  } else {
    caja.innerHTML = h`<span>Vuelto</span><b>${dinero(recibido - total)}</b>`;
  }
}

function cambiarMetodo(metodo) {
  estado.metodoPago = metodo;
  guardarEstado();
  pintarPago();
}

function pintarPago() {
  $$('#metodos button', raiz).forEach((b) => b.classList.toggle('activo', b.dataset.metodo === estado.metodoPago));
  const efectivo = estado.metodoPago === 'Efectivo';
  $('#bloqueEfectivo', raiz).hidden = !efectivo;
  $('#bloqueReferencia', raiz).hidden = efectivo;
  $('#recibido', raiz).value = estado.recibido;
  $('#referencia', raiz).value = estado.referencia;
}

// ---------- Cliente ----------

function pintarCliente() {
  $('#cliNombre', raiz).value = estado.cliente.nombre;
  $('#cliRuc', raiz).value = estado.cliente.ruc;
  $('#cliRuc', raiz).classList.remove('invalido');
  $('#cliDireccion', raiz).value = estado.cliente.direccion;
  $('#cliGuardar', raiz).checked = estado.cliente.guardar;
  pintarGuardarCliente();
}

function pintarGuardarCliente() {
  const nombre = estado.cliente.nombre.trim().toUpperCase();
  $('#cajaGuardar', raiz).hidden = !nombre || nombre === CONSUMIDOR_FINAL || estado.cliente.id !== null;
}

const buscarClientes = debounce(async () => {
  if (!raiz?.isConnected) return;
  const texto = $('#cliNombre', raiz).value.trim();
  if (texto.length < 2) { cerrarClientes(); return; }
  try {
    clientes = await api.get(conParametros('/api/clientes', { texto, limite: 8 }));
  } catch {
    clientes = [];
  }
  indiceCliente = clientes.length ? 0 : -1;
  const lista = $('#cliSugerencias', raiz);
  if (!clientes.length || document.activeElement !== $('#cliNombre', raiz)) { lista.hidden = true; return; }
  lista.innerHTML = h`${clientes.map((c, i) => h`
    <div class="sugerencia ${i === indiceCliente ? 'activa' : ''}" data-i="${i}">
      <div class="sug-principal">${c.nombre}</div>
      <div class="sug-detalle"><span>${c.ruc || 'Sin RUC'}</span><span>${c.direccion || ''}</span></div>
    </div>`)}`;
  lista.hidden = false;
}, 200);

function teclaCliente(e) {
  const lista = $('#cliSugerencias', raiz);
  const abierta = !lista.hidden && clientes.length > 0;
  if (e.key === 'ArrowDown' && abierta) {
    e.preventDefault();
    indiceCliente = (indiceCliente + 1) % clientes.length;
  } else if (e.key === 'ArrowUp' && abierta) {
    e.preventDefault();
    indiceCliente = (indiceCliente - 1 + clientes.length) % clientes.length;
  } else if (e.key === 'Enter') {
    e.preventDefault();
    if (abierta && indiceCliente >= 0) seleccionarCliente(clientes[indiceCliente]);
    else enfocar('#cliRuc');
    return;
  } else if (e.key === 'Escape' && abierta) {
    e.preventDefault();
    e.stopPropagation();
    cerrarClientes();
    return;
  } else {
    return;
  }
  $$('.sugerencia', lista).forEach((el, i) => el.classList.toggle('activa', i === indiceCliente));
}

function seleccionarCliente(cliente) {
  estado.cliente = { id: cliente.id, nombre: cliente.nombre, ruc: cliente.ruc || '', direccion: cliente.direccion || '', guardar: false };
  guardarEstado();
  pintarCliente();
  cerrarClientes();
  enfocar('#busqueda');
}

function cerrarClientes() {
  if (!raiz?.isConnected) return;
  $('#cliSugerencias', raiz).hidden = true;
}

// ---------- Pantalla ----------

function pintarTodo() {
  pintarModo();
  pintarCliente();
  pintarLineas();
  pintarPago();
  pintarTotales();
  pintarUltima();
}

function pintarModo() {
  const editando = modificando();
  $('#tituloFactura', raiz).textContent = editando ? 'Modificando factura' : 'Factura';
  $('#btnLimpiar', raiz).innerHTML = h`${editando ? 'Cancelar modificación' : 'Limpiar'} <kbd>Esc</kbd>`;
  $('#btnCobrar', raiz).innerHTML = h`${editando ? 'Guardar cambios' : 'Generar factura'} <kbd>F10</kbd>`;
  const banda = $('#bandaModificacion', raiz);
  banda.hidden = !editando;
  if (editando) {
    banda.innerHTML = h`<span>Está corrigiendo la factura <b>${estado.numeroCompleto}</b>, ya emitida. Al guardar se mostrarán
      los cambios y se pedirán el motivo y el nombre del empleado.</span>
      <button type="button" class="btn btn-sm" data-cancelar>Cancelar modificación</button>`;
    $('#numFactura', raiz).textContent = estado.numeroCompleto;
    $('#avisoNumeracion', raiz).hidden = true;
  }
  pintarInfoTalonario();
}

function pintarInfoTalonario() {
  const numero = modificando() ? estado.numero : proximo?.numero;
  $('#infoTalonario', raiz).textContent = numero ? `Se imprime en el formulario No. ${numero} del talonario` : '';
}

async function actualizarNumero() {
  if (modificando()) return;
  try {
    proximo = await api.get('/api/facturas/proximo-numero');
    if (!raiz.isConnected || modificando()) return;
    $('#numFactura', raiz).textContent = `Serie ${proximo.serie} · No. ${proximo.numero}`;
    const caja = $('#avisoNumeracion', raiz);
    caja.hidden = !proximo.advertencia;
    caja.textContent = proximo.advertencia || '';
    pintarInfoTalonario();
  } catch {
    $('#numFactura', raiz).textContent = 'Sin numeración';
  }
}

/** Vuelve a la factura nueva que se estaba armando antes de modificar. */
function terminarModificacion() {
  guardarLocal(CLAVE_MODIFICACION, null);
  estado = normalizarEstado(leerLocal(CLAVE_ESTADO));
  limpiarBusqueda();
  pintarTodo();
  actualizarNumero();
  enfocar('#busqueda');
}

async function limpiar() {
  if (modificando()) {
    if (await confirmar(`¿Cancelar la modificación de la factura ${estado.numeroCompleto}? Los cambios no se guardan.`,
      { titulo: 'Cancelar modificación', textoSi: 'Sí, cancelar', peligro: true })) {
      terminarModificacion();
    }
    return;
  }
  const hayDatos = estado.lineas.length > 0 || estado.cliente.nombre.trim();
  if (hayDatos && !(await confirmar('¿Borrar la factura en curso?', { titulo: 'Limpiar factura', textoSi: 'Borrar', peligro: true }))) {
    return;
  }
  estado = estadoVacio();
  guardarEstado();
  limpiarBusqueda();
  pintarTodo();
  enfocar('#busqueda');
}

function articuloLibre() {
  if (estado.lineas.length >= maximoLineas()) {
    aviso(`El talonario tiene ${maximoLineas()} líneas.`, 'error');
    return;
  }
  abrirModal({
    titulo: 'Artículo libre (no registrado en el catálogo)',
    cuerpo: h`
      <div class="rejilla">
        <div class="campo completo"><label>Descripción</label><input id="libreDescripcion" maxlength="200" autocomplete="off"></div>
        <div class="campo"><label>Cantidad</label><input id="libreCantidad" class="num" inputmode="decimal" value="1"></div>
        <div class="campo"><label>Unidad</label>
          <select id="libreUnidad">${unidades.map((u) => h`<option value="${u}">${u}</option>`)}</select></div>
        <div class="campo"><label>Precio unitario</label><input id="librePrecio" class="num" inputmode="decimal" placeholder="0.00"></div>
      </div>
      <p class="muy-suave" style="margin:10px 0 0">No descuenta existencias. Para venderlo seguido, regístrelo en Productos.</p>`,
    botones: [
      { texto: 'Cancelar' },
      {
        texto: 'Agregar', clase: 'btn-primario', primario: true,
        accion: (modal) => {
          const descripcion = $('#libreDescripcion', modal.cuerpo).value.trim();
          const cantidad = aNumero($('#libreCantidad', modal.cuerpo).value);
          const precio = aNumero($('#librePrecio', modal.cuerpo).value);
          if (!descripcion) throw new Error('Escriba la descripción del artículo.');
          if (!(cantidad > 0)) throw new Error('La cantidad debe ser mayor que cero.');
          if (!(precio >= 0)) throw new Error('Indique un precio válido.');
          estado.lineas.push({
            productoId: null,
            codigo: '',
            descripcion: descripcion.toUpperCase(),
            unidad: $('#libreUnidad', modal.cuerpo).value || 'UND',
            cantidad: redondear(cantidad, 3),
            precio: redondear(precio),
            stock: null
          });
          guardarEstado();
          pintarLineas(estado.lineas.length - 1);
          pintarTotales();
        }
      }
    ],
    alCerrar: () => setTimeout(() => enfocar('#busqueda'), 0)
  });
}

// ---------- Generar / guardar cambios ----------

/** Validaciones comunes antes de generar o de guardar una modificación. */
function validarAntesDeGuardar() {
  if (!estado.lineas.length) {
    aviso('Agregue al menos un producto.', 'aviso');
    enfocar('#busqueda');
    return null;
  }
  const invalida = estado.lineas.findIndex((l) => !(l.cantidad > 0) || !(l.precio >= 0));
  if (invalida >= 0) { aviso(`Revise la cantidad o el precio de la línea ${invalida + 1}.`, 'error'); return null; }
  if (estado.lineas.length > maximoLineas()) {
    aviso(`El talonario tiene ${maximoLineas()} líneas. Divida la venta en dos facturas.`, 'error', 7000);
    return null;
  }
  if (!revisarRuc()) {
    aviso('Corrija el RUC o la cédula del cliente (o déjelo vacío).', 'error', 6000);
    enfocar('#cliRuc');
    return null;
  }
  const { total } = calcularTotales();
  const efectivo = estado.metodoPago === 'Efectivo';
  const recibido = estado.recibido.trim() === '' ? total : aNumero(estado.recibido);
  if (efectivo && !(recibido >= total)) {
    aviso('El efectivo recibido es menor al total.', 'error');
    enfocar('#recibido');
    return null;
  }
  return { total, recibido: efectivo ? recibido : null };
}

function solicitud(recibido) {
  return {
    cliente: {
      id: estado.cliente.id,
      nombre: estado.cliente.nombre,
      ruc: estado.cliente.ruc,
      direccion: estado.cliente.direccion,
      guardar: estado.cliente.guardar
    },
    lineas: estado.lineas.map((l) => ({
      productoId: l.productoId,
      descripcion: l.descripcion,
      unidad: l.unidad,
      cantidad: l.cantidad,
      precio: l.precio
    })),
    metodoPago: estado.metodoPago,
    montoRecibido: recibido,
    referencia: estado.referencia
  };
}

async function cobrar() {
  if (cobrando) return;
  // Un producto escaneado justo antes de F10 debe entrar en la factura.
  if (pendientes.size) await Promise.allSettled([...pendientes]);
  if (modificando()) return guardarModificacion();

  const datos = validarAntesDeGuardar();
  if (!datos) return;
  if (!(await confirmarCobro(datos.total, datos.recibido))) return;

  cobrando = true;
  $('#btnCobrar', raiz).disabled = true;
  try {
    const factura = await api.post('/api/facturas', solicitud(datos.recibido));
    registrarUltima(factura);
    estado = estadoVacio();
    guardarEstado();
    pintarTodo();
    actualizarNumero();
    actualizarAlertas();
    aviso(`Factura ${factura.numeroCompleto} generada.`, 'ok', 5000);
    await dialogoImprimir(factura);
  } catch (error) {
    aviso(error.message, 'error', 8000);
  } finally {
    cobrando = false;
    if (raiz.isConnected) {
      $('#btnCobrar', raiz).disabled = false;
      enfocar('#busqueda');
    }
  }
}

async function guardarModificacion() {
  const datos = validarAntesDeGuardar();
  if (!datos) return;
  const cuerpo = solicitud(datos.recibido);

  cobrando = true;
  $('#btnCobrar', raiz).disabled = true;
  try {
    const { cambios } = await api.post(`/api/facturas/${estado.facturaId}/cambios`, cuerpo);
    if (!cambios.length) {
      if (await confirmar('No se realizaron cambios en la factura. ¿Salir de la modificación?',
        { titulo: 'Sin cambios', textoSi: 'Salir' })) {
        terminarModificacion();
      }
      return;
    }

    const notas = await pedirNotasModificacion(cambios);
    if (!notas) return;

    const factura = await api.put(`/api/facturas/${estado.facturaId}`, { ...cuerpo, ...notas });
    registrarUltima(factura);
    terminarModificacion();
    actualizarAlertas();
    aviso(`Factura ${factura.numeroCompleto} modificada.`, 'ok', 5000);
    await dialogoImprimir(factura, { titulo: `Imprimir factura modificada ${factura.numeroCompleto}` });
  } catch (error) {
    aviso(error.message, 'error', 8000);
  } finally {
    cobrando = false;
    if (raiz.isConnected) {
      $('#btnCobrar', raiz).disabled = false;
      enfocar('#busqueda');
    }
  }
}

/** Muestra los cambios detectados y pide las notas obligatorias. Devuelve { motivo, empleado } o null. */
function pedirNotasModificacion(cambios) {
  return new Promise((resolver) => {
    let notas = null;
    abrirModal({
      titulo: `Confirmar modificación de ${estado.numeroCompleto}`,
      cuerpo: h`
        <p style="margin-top:0">Cambios detectados:</p>
        <ul class="lista-cambios">${cambios.map((c) => h`<li>${c}</li>`)}</ul>
        <div class="campo"><label for="motivoModificacion">Motivo de la modificación *</label>
          <textarea id="motivoModificacion" rows="3" maxlength="300" placeholder="Explique por qué se corrige la factura"></textarea></div>
        <div class="campo" style="margin-top:10px"><label for="empleadoModificacion">Nombre del empleado que modifica *</label>
          <input id="empleadoModificacion" maxlength="80" autocomplete="off"></div>`,
      botones: [
        { texto: 'Volver' },
        {
          texto: 'Confirmar modificación', clase: 'btn-primario', primario: true,
          accion: (modal) => {
            const motivo = $('#motivoModificacion', modal.cuerpo);
            const empleado = $('#empleadoModificacion', modal.cuerpo);
            const faltaMotivo = motivo.value.trim().length < 5;
            const faltaEmpleado = empleado.value.trim().length < 3;
            motivo.classList.toggle('invalido', faltaMotivo);
            empleado.classList.toggle('invalido', faltaEmpleado);
            if (faltaMotivo || faltaEmpleado) {
              aviso('El motivo (mínimo 5 caracteres) y el nombre del empleado son obligatorios.', 'error', 6000);
              (faltaMotivo ? motivo : empleado).focus();
              return false;
            }
            notas = { motivo: motivo.value.trim(), empleado: empleado.value.trim() };
          }
        }
      ],
      alCerrar: () => resolver(notas)
    });
  });
}

function confirmarCobro(total, recibido) {
  return new Promise((resolver) => {
    let confirmado = false;
    const efectivo = recibido !== null;
    abrirModal({
      titulo: 'Confirmar factura',
      ancho: 'sm',
      cuerpo: h`
        <div class="resumen-cobro">
          <div class="fila"><span>Cliente</span><b>${estado.cliente.nombre.trim().toUpperCase() || CONSUMIDOR_FINAL}</b></div>
          <div class="fila"><span>Productos</span><b>${estado.lineas.length} ${estado.lineas.length === 1 ? 'línea' : 'líneas'}</b></div>
          <div class="fila"><span>Forma de pago</span><b>${estado.metodoPago}</b></div>
          <div class="gran-total"><span>TOTAL</span><b>${dinero(total)}</b></div>
          ${efectivo ? h`
            <div class="fila"><span>Recibido</span><b>${dinero(recibido)}</b></div>
            <div class="fila vuelto-grande"><span>Vuelto</span><b>${dinero(recibido - total)}</b></div>` : ''}
          ${proximo ? h`<div class="fila"><span>Formulario del talonario</span><b>No. ${proximo.numero}</b></div>` : ''}
        </div>`,
      botones: [
        { texto: 'Volver' },
        { texto: 'Generar  (Enter)', clase: 'btn-exito', primario: true, accion: () => { confirmado = true; } }
      ],
      alCerrar: () => resolver(confirmado)
    });
  });
}

// ---------- Última factura ----------

function registrarUltima(factura) {
  ultima = { id: factura.id, numero: factura.numero, numeroCompleto: factura.numeroCompleto, total: factura.total, vuelto: factura.vuelto };
  guardarLocal(CLAVE_ULTIMA, ultima);
}

function pintarUltima() {
  const caja = $('#ultima', raiz);
  caja.hidden = !ultima;
  if (!ultima) return;
  caja.innerHTML = h`
    <div class="ultima-titulo">Última factura</div>
    <div class="ultima-num">${ultima.numeroCompleto || ultima.numero} · ${dinero(ultima.total)}${ultima.vuelto ? h` · vuelto ${dinero(ultima.vuelto)}` : ''}</div>
    <div class="acciones">
      <button type="button" class="btn btn-sm" data-accion="imprimir">Imprimir</button>
      <button type="button" class="btn btn-sm" data-accion="ver">Vista previa</button>
      ${esEscritorio ? h`<button type="button" class="btn btn-sm" data-accion="pdf">PDF</button>` : ''}
    </div>`;
}

async function accionUltima(e) {
  const boton = e.target.closest('[data-accion]');
  if (!boton || !ultima) return;
  const titulo = `Factura ${ultima.numeroCompleto || ultima.numero}`;
  try {
    if (boton.dataset.accion === 'imprimir') {
      await imprimir(rutaFactura(ultima.id));
      aviso(`${titulo} enviada a la impresora.`);
    } else if (boton.dataset.accion === 'ver') {
      vistaPrevia(rutaVista(ultima.id, true), titulo, { rutaImpresion: rutaFactura(ultima.id), textoImprimir: 'Imprimir en talonario' });
    } else if (boton.dataset.accion === 'pdf') {
      await guardarPdf(rutaVista(ultima.id), `Factura-${ultima.numeroCompleto || ultima.numero}.pdf`);
    }
  } catch (error) {
    aviso(error.message, 'error', 7000);
  }
}
