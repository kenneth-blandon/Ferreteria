// Arranque: navegación entre módulos (#/ruta), atajos globales y datos de la barra lateral.
import { $, $$, h, hayModal, fecha } from './ui.js';
import { api } from './api.js';
import { obtenerConfiguracion } from './config.js';
import { esEscritorio } from './impresion.js';

// Cada módulo exporta montar(contenedor, parametros) y puede devolver { teclas(tecla), desmontar() }.
const rutas = {
  facturar: { titulo: 'Facturar', cargar: () => import('./modulos/facturacion.js') },
  productos: { titulo: 'Productos', cargar: () => import('./modulos/productos.js') },
  clientes: { titulo: 'Clientes', cargar: () => import('./modulos/clientes.js') },
  historial: { titulo: 'Historial', cargar: () => import('./modulos/historial.js') },
  cierre: { titulo: 'Cierre de caja', cargar: () => import('./modulos/cierre.js') },
  configuracion: { titulo: 'Configuración', cargar: () => import('./modulos/configuracion.js') }
};
const orden = Object.keys(rutas);

let actual = null;
let turno = 0;

async function navegar() {
  const [nombre, consulta] = location.hash.replace(/^#\/?/, '').split('?');
  const ruta = rutas[nombre] ? nombre : 'facturar';
  const miTurno = ++turno;

  try { actual?.desmontar?.(); } catch { /* el módulo anterior ya no importa */ }
  actual = null;

  $$('#menu a').forEach((a) => a.classList.toggle('activo', a.dataset.ruta === ruta));
  document.title = `${rutas[ruta].titulo} · Facturación`;
  const vista = $('#vista');
  vista.innerHTML = '<div class="cargando">Cargando…</div>';

  try {
    const modulo = await rutas[ruta].cargar();
    if (miTurno !== turno) return;
    // Cada módulo recibe su propio contenedor: al salir del módulo queda desconectado y sus
    // respuestas tardías (raiz.isConnected === false) ya no tocan la pantalla nueva.
    const contenedor = document.createElement('div');
    contenedor.className = 'modulo';
    vista.replaceChildren(contenedor);
    const resultado = await modulo.montar(contenedor, new URLSearchParams(consulta || ''));
    if (miTurno === turno) actual = resultado || {};
  } catch (error) {
    if (miTurno === turno) vista.innerHTML = h`<div class="error-carga">No se pudo abrir ${rutas[ruta].titulo}: ${error.message}</div>`;
  }
  actualizarAlertas();
}

/** Nombre normalizado de una tecla: "F10", "Alt+1", "Ctrl+Enter", "Escape". */
function nombreTecla(evento) {
  const partes = [];
  if (evento.ctrlKey) partes.push('Ctrl');
  if (evento.altKey) partes.push('Alt');
  partes.push(evento.key.length === 1 ? evento.key.toUpperCase() : evento.key);
  return partes.join('+');
}

function atenderTecla(tecla) {
  const alt = /^Alt\+([1-6])$/.exec(tecla);
  if (alt) {
    location.hash = `#/${orden[Number(alt[1]) - 1]}`;
    return true;
  }
  if (hayModal()) return false;
  return Boolean(actual?.teclas?.(tecla));
}

document.addEventListener('keydown', (evento) => {
  if (evento.repeat && evento.key.startsWith('F')) return;
  if (atenderTecla(nombreTecla(evento))) evento.preventDefault();
});
document.addEventListener('tecla-host', (evento) => atenderTecla(evento.detail));

/** Avisa en la barra lateral cuántos productos están en stock mínimo. */
export async function actualizarAlertas() {
  try {
    const alertas = await api.get('/api/inventario/alertas');
    const enlace = $('#alertaStock');
    enlace.hidden = alertas.length === 0;
    enlace.querySelector('span').textContent =
      alertas.length === 1 ? '1 producto con stock bajo' : `${alertas.length} productos con stock bajo`;
    enlace.title = alertas.slice(0, 15).map((p) => `${p.nombre}: ${p.stock} ${p.unidad}`).join('\n');
  } catch { /* la alerta es informativa */ }
}

async function iniciar() {
  $('#fechaHoy').textContent = `${fecha(new Date())} · ${esEscritorio ? 'Escritorio' : 'Navegador'}`;
  try {
    const { negocio } = await obtenerConfiguracion();
    $('#marcaNombre').textContent = negocio.nombreComercial || 'Facturación';
    $('#marcaGiro').textContent = negocio.giro || 'Ferretería';
  } catch { /* se muestran los textos por defecto */ }
  window.addEventListener('hashchange', navegar);
  navegar();
}

iniciar();
