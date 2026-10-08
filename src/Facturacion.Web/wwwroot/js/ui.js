// Utilidades de interfaz: plantillas HTML seguras, formato de montos, ventanas modales y avisos.

// ---------- Plantillas HTML (escapan automáticamente los valores) ----------

class HtmlSeguro {
  constructor(texto) { this.texto = texto; }
  toString() { return this.texto; }
}

const ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
export const escapar = (texto) => String(texto).replace(/[&<>"']/g, (c) => ESCAPES[c]);

function convertir(valor) {
  if (valor === null || valor === undefined || valor === false || valor === true) return '';
  if (valor instanceof HtmlSeguro) return valor.texto;
  if (Array.isArray(valor)) return valor.map(convertir).join('');
  return escapar(valor);
}

/** Plantilla etiquetada: h`<b>${nombre}</b>` escapa "nombre"; las plantillas anidadas se respetan. */
export function h(partes, ...valores) {
  let resultado = '';
  partes.forEach((parte, i) => {
    resultado += parte;
    if (i < valores.length) resultado += convertir(valores[i]);
  });
  return new HtmlSeguro(resultado);
}

/** Inserta HTML sin escapar (solo para contenido generado por la propia aplicación). */
export const crudo = (texto) => new HtmlSeguro(String(texto));

export const $ = (selector, raiz = document) => raiz.querySelector(selector);
export const $$ = (selector, raiz = document) => [...raiz.querySelectorAll(selector)];

// ---------- Números y fechas ----------

const formato2 = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const formatoCantidad = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });

/** Redondeo igual al del servidor (0.005 sube). */
export function redondear(valor, decimales = 2) {
  const factor = 10 ** decimales;
  const r = Math.round(Math.abs(valor) * factor + 1e-7) / factor;
  return valor < 0 ? -r : r;
}

export const dinero = (valor) => 'C$ ' + formato2.format(redondear(Number(valor) || 0));
export const numero2 = (valor) => formato2.format(redondear(Number(valor) || 0));
export const cantidad = (valor) => formatoCantidad.format(redondear(Number(valor) || 0, 3));

/** Valor para un campo editable: sin separador de miles. */
export const valorEntrada = (valor, decimales = 2) =>
  decimales === 2 ? redondear(Number(valor) || 0).toFixed(2) : String(redondear(Number(valor) || 0, decimales));

/** Interpreta "1,250.50", "1.250,50", "12,5" o "C$ 300". Devuelve NaN si no es número. */
export function aNumero(valor) {
  if (typeof valor === 'number') return valor;
  let s = String(valor ?? '').trim().replace(/C\$|\$|\s/gi, '');
  if (!s) return NaN;
  const coma = s.lastIndexOf(',');
  const punto = s.lastIndexOf('.');
  if (coma >= 0 && punto >= 0) {
    s = coma > punto ? s.replace(/\./g, '').replace(',', '.') : s.replace(/,/g, '');
  } else if (coma >= 0) {
    const decimales = s.length - coma - 1;
    const unaComa = s.indexOf(',') === coma;
    const entero = s.slice(0, coma).replace(/^[-+]/, '');
    s = unaComa && (decimales !== 3 || entero === '' || entero === '0') ? s.replace(',', '.') : s.replace(/,/g, '');
  }
  return /^[-+]?\d*\.?\d+$/.test(s) ? Number(s) : NaN;
}

const dos = (n) => String(n).padStart(2, '0');
export function fecha(valor) {
  const d = new Date(valor);
  return isNaN(d) ? '' : `${dos(d.getDate())}/${dos(d.getMonth() + 1)}/${d.getFullYear()}`;
}
export function fechaHora(valor) {
  const d = new Date(valor);
  return isNaN(d) ? '' : `${fecha(d)} ${dos(d.getHours())}:${dos(d.getMinutes())}`;
}
export function hora(valor) {
  const d = new Date(valor);
  return isNaN(d) ? '' : `${dos(d.getHours())}:${dos(d.getMinutes())}`;
}
/** Fecha local en formato yyyy-mm-dd (para inputs de tipo date y la API). */
export function hoyIso(desplazamientoDias = 0) {
  const d = new Date();
  d.setDate(d.getDate() + desplazamientoDias);
  return `${d.getFullYear()}-${dos(d.getMonth() + 1)}-${dos(d.getDate())}`;
}

export function debounce(funcion, milisegundos) {
  let temporizador;
  return (...argumentos) => {
    clearTimeout(temporizador);
    temporizador = setTimeout(() => funcion(...argumentos), milisegundos);
  };
}

/** Descarga un archivo (en la versión de escritorio abre "Guardar como"). */
export function descargar(url) {
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = '';
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
}

/** Lee/guarda JSON en el almacenamiento local sin romper la aplicación si no está disponible. */
export function leerLocal(clave, predeterminado = null) {
  try {
    const texto = localStorage.getItem(clave);
    return texto ? JSON.parse(texto) : predeterminado;
  } catch {
    return predeterminado;
  }
}
export function guardarLocal(clave, valor) {
  try {
    if (valor === null || valor === undefined) localStorage.removeItem(clave);
    else localStorage.setItem(clave, JSON.stringify(valor));
  } catch { /* sin almacenamiento local */ }
}

// ---------- Avisos ----------

export function aviso(mensaje, tipo = 'ok', milisegundos = 3500) {
  const contenedor = document.getElementById('avisos');
  const elemento = document.createElement('div');
  elemento.className = `notificacion ${tipo === 'aviso' ? 'advertencia' : tipo}`;
  elemento.textContent = mensaje;
  elemento.addEventListener('click', () => elemento.remove());
  contenedor.append(elemento);
  while (contenedor.children.length > 3) contenedor.firstElementChild.remove();
  setTimeout(() => elemento.remove(), milisegundos);
}

// ---------- Ventanas modales ----------

const pila = [];
export const hayModal = () => pila.length > 0;

/** Cierra todas las ventanas abiertas (al cambiar de módulo). */
export function cerrarModales() {
  while (pila.length) pila.at(-1).cerrar();
}

/**
 * Abre una ventana modal.
 * botones: [{ texto, clase, primario, accion(modal) }] — si la acción devuelve false la ventana queda abierta.
 * Esc cierra; Enter ejecuta el botón primario.
 */
export function abrirModal({ titulo, cuerpo, botones = [], ancho = 'md', alCerrar } = {}) {
  const fondo = document.createElement('div');
  fondo.className = 'modal-fondo';
  fondo.innerHTML = h`
    <div class="modal modal-${ancho}" role="dialog" aria-modal="true">
      <header class="modal-cabecera"><h2>${titulo}</h2><button type="button" class="btn-icono" data-cerrar title="Cerrar (Esc)">✕</button></header>
      <div class="modal-cuerpo"></div>
      <footer class="modal-pie"></footer>
    </div>`;
  const cuerpoEl = fondo.querySelector('.modal-cuerpo');
  if (cuerpo instanceof Node) cuerpoEl.append(cuerpo);
  else if (cuerpo !== undefined) cuerpoEl.innerHTML = cuerpo;

  let cerrado = false;
  const modal = {
    elemento: fondo,
    cuerpo: cuerpoEl,
    cerrar() {
      if (cerrado) return;
      cerrado = true;
      fondo.remove();
      const i = pila.indexOf(modal);
      if (i >= 0) pila.splice(i, 1);
      alCerrar?.();
    },
    ocupado(valor) {
      fondo.querySelectorAll('.modal-pie button').forEach((b) => { b.disabled = valor; });
    },
    primario: null
  };

  const pie = fondo.querySelector('.modal-pie');
  for (const boton of botones) {
    const b = document.createElement('button');
    b.type = 'button';
    b.className = `btn ${boton.clase || ''} ${boton.izquierda ? 'izquierda' : ''}`;
    b.textContent = boton.texto;
    b.addEventListener('click', async () => {
      if (!boton.accion) return modal.cerrar();
      try {
        modal.ocupado(true);
        const resultado = await boton.accion(modal);
        if (resultado !== false) modal.cerrar();
      } catch (error) {
        aviso(error.message, 'error', 6000);
      } finally {
        if (!cerrado) modal.ocupado(false);
      }
    });
    pie.append(b);
    if (boton.primario) modal.primario = b;
  }

  fondo.querySelector('[data-cerrar]').addEventListener('click', () => modal.cerrar());
  fondo.addEventListener('mousedown', (e) => { if (e.target === fondo) fondo.dataset.clicFuera = '1'; });
  fondo.addEventListener('mouseup', (e) => {
    if (e.target === fondo && fondo.dataset.clicFuera) modal.cerrar();
    delete fondo.dataset.clicFuera;
  });

  pila.push(modal);
  document.body.append(fondo);
  const enfocable = cuerpoEl.querySelector('input:not([type=hidden]):not([disabled]), select, textarea') || modal.primario;
  enfocable?.focus();
  if (enfocable?.select) enfocable.select();
  return modal;
}

document.addEventListener('keydown', (e) => {
  const modal = pila.at(-1);
  if (!modal) return;
  if (e.key === 'Escape') {
    e.preventDefault();
    e.stopImmediatePropagation();
    modal.cerrar();
  } else if (e.key === 'Enter' && !e.shiftKey) {
    const activo = document.activeElement;
    const propio = activo?.tagName === 'TEXTAREA' || activo?.tagName === 'BUTTON' || activo?.closest?.('[data-enter-propio]');
    if (!propio && modal.primario && !modal.primario.disabled) {
      e.preventDefault();
      e.stopImmediatePropagation();
      modal.primario.click();
    }
  } else if (/^F\d+$/.test(e.key)) {
    // Con una ventana abierta, las teclas de función no deben actuar sobre la pantalla de fondo.
    e.preventDefault();
    e.stopImmediatePropagation();
  }
});

/** Pregunta Sí/No. */
export function confirmar(mensaje, { titulo = 'Confirmar', textoSi = 'Aceptar', peligro = false } = {}) {
  return new Promise((resolver) => {
    let respuesta = false;
    abrirModal({
      titulo,
      ancho: 'sm',
      cuerpo: h`<p style="margin:0">${mensaje}</p>`,
      botones: [
        { texto: 'Cancelar' },
        { texto: textoSi, clase: peligro ? 'btn-peligro' : 'btn-primario', primario: true, accion: () => { respuesta = true; } }
      ],
      alCerrar: () => resolver(respuesta)
    });
  });
}

/** Pide un texto. Devuelve null si se cancela. */
export function pedirTexto({ titulo, mensaje = '', etiqueta = '', valor = '', requerido = true, textoSi = 'Aceptar', peligro = false }) {
  return new Promise((resolver) => {
    let respuesta = null;
    abrirModal({
      titulo,
      ancho: 'sm',
      cuerpo: h`${mensaje ? h`<p style="margin:0 0 10px">${mensaje}</p>` : ''}
        <div class="campo"><label>${etiqueta}</label><input id="textoPedido" value="${valor}" autocomplete="off"></div>`,
      botones: [
        { texto: 'Cancelar' },
        {
          texto: textoSi, clase: peligro ? 'btn-peligro' : 'btn-primario', primario: true,
          accion: (modal) => {
            const entrada = modal.cuerpo.querySelector('#textoPedido');
            if (requerido && !entrada.value.trim()) {
              entrada.classList.add('invalido');
              entrada.focus();
              return false;
            }
            respuesta = entrada.value.trim();
          }
        }
      ],
      alCerrar: () => resolver(respuesta)
    });
  });
}

/** Lee los campos con atributo name de un contenedor como objeto. */
export function leerFormulario(raiz) {
  const datos = {};
  raiz.querySelectorAll('[name]').forEach((campo) => {
    datos[campo.name] = campo.type === 'checkbox' ? campo.checked : campo.value;
  });
  return datos;
}
