// Impresión. En la versión de escritorio se pide a la ventana (C#) que imprima directo en la
// impresora configurada, sin cuadro de diálogo. En el navegador se usa el cuadro de impresión.
import { obtenerConfiguracion } from './config.js';
import { abrirModal, h, aviso } from './ui.js';

const webview = window.chrome?.webview;
export const esEscritorio = Boolean(webview);

const pendientes = new Map();
let secuencia = 0;

if (webview) {
  webview.addEventListener('message', (evento) => {
    const mensaje = evento.data;
    if (mensaje?.tipo === 'respuesta') {
      const pendiente = pendientes.get(mensaje.id);
      if (!pendiente) return;
      pendientes.delete(mensaje.id);
      if (mensaje.ok) pendiente.resolver(mensaje);
      else pendiente.rechazar(new Error(mensaje.mensaje || 'No se pudo imprimir.'));
    } else if (mensaje?.tipo === 'tecla') {
      // Teclas que Windows reserva (F10, Alt+número) llegan desde la ventana.
      document.dispatchEvent(new CustomEvent('tecla-host', { detail: mensaje.tecla }));
    } else if (mensaje?.tipo === 'descarga') {
      aviso(mensaje.mensaje, mensaje.ok ? 'ok' : 'error', 6000);
    }
  });
}

function enviarAVentana(mensaje) {
  return new Promise((resolver, rechazar) => {
    const id = ++secuencia;
    pendientes.set(id, { resolver, rechazar });
    webview.postMessage({ ...mensaje, id });
    setTimeout(() => {
      if (pendientes.delete(id)) rechazar(new Error('La impresora no respondió a tiempo.'));
    }, 120000);
  });
}

export const rutaFactura = (id, formato, reimpresion = false) =>
  `/imprimir/factura/${id}?formato=${encodeURIComponent(formato)}${reimpresion ? '&reimpresion=true' : ''}`;

/** Imprime un documento de /imprimir/... según la configuración. */
export async function imprimir(ruta) {
  const { impresion } = await obtenerConfiguracion();
  if (webview && !impresion.mostrarDialogo) return enviarAVentana({ accion: 'imprimir', ruta });
  return imprimirConDialogo(ruta);
}

/** Abre el cuadro de impresión del navegador con el documento indicado. */
export function imprimirConDialogo(ruta) {
  return new Promise((resolver, rechazar) => {
    const marco = document.createElement('iframe');
    marco.className = 'marco-impresion';
    marco.addEventListener('load', () => {
      try {
        marco.contentWindow.focus();
        marco.contentWindow.print();
        resolver();
      } catch (error) {
        rechazar(error);
      } finally {
        setTimeout(() => marco.remove(), 30000);
      }
    });
    marco.src = ruta;
    document.body.append(marco);
  });
}

/** Guarda el documento como PDF (escritorio) o lo abre en otra pestaña (navegador). */
export async function guardarPdf(ruta, nombreArchivo) {
  if (webview) return enviarAVentana({ accion: 'pdf', ruta, archivo: nombreArchivo });
  window.open(ruta, '_blank');
  return null;
}

/** Muestra el documento tal como se imprimirá. */
export function vistaPrevia(ruta, titulo) {
  abrirModal({
    titulo,
    ancho: 'xl',
    cuerpo: h`<iframe class="marco-vista" src="${ruta}" title="${titulo}"></iframe>`,
    botones: [
      { texto: 'Cerrar' },
      {
        texto: 'Imprimir', clase: 'btn-primario', primario: true,
        accion: async () => {
          await imprimir(ruta);
          aviso('Documento enviado a la impresora.');
        }
      }
    ]
  });
}
