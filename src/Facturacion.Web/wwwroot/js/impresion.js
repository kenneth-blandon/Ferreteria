// Impresión en el talonario preimpreso. En la versión de escritorio se pide a la ventana (C#) que
// imprima directo en la impresora configurada, sin cuadro de diálogo. En el navegador se usa el
// cuadro de impresión. La hoja carta es solo vista previa (y PDF).
import { obtenerConfiguracion } from './config.js';
import { abrirModal, h, aviso, dinero } from './ui.js';

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

/** Datos de la factura sobre el formulario preimpreso (lo que se envía a la impresora). */
export const rutaFactura = (id) => `/imprimir/factura/${id}`;

/** Vista previa en hoja carta: el talonario completo con los datos. */
export const rutaVista = (id, reimpresion = false) => `/imprimir/factura/${id}/vista${reimpresion ? '?reimpresion=true' : ''}`;

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

/**
 * Muestra un documento tal como se verá. Si se indica rutaImpresion aparece el botón para
 * imprimirlo (por ejemplo, la factura en el talonario).
 */
export function vistaPrevia(ruta, titulo, { rutaImpresion, textoImprimir = 'Imprimir' } = {}) {
  abrirModal({
    titulo,
    ancho: 'xl',
    cuerpo: h`<iframe class="marco-vista" src="${ruta}" title="${titulo}"></iframe>`,
    botones: [
      { texto: 'Cerrar', primario: !rutaImpresion },
      ...(rutaImpresion
        ? [{
            texto: textoImprimir, clase: 'btn-primario', primario: true,
            accion: async () => {
              await imprimir(rutaImpresion);
              aviso('Documento enviado a la impresora.');
            }
          }]
        : [])
    ]
  });
}

/**
 * Paso final al generar o corregir una factura: indica qué formulario del talonario colocar
 * en la impresora y espera la orden de imprimir. Devuelve true si se imprimió.
 */
export function dialogoImprimir(factura, { titulo } = {}) {
  return new Promise((resolver) => {
    let impreso = false;
    abrirModal({
      titulo: titulo || `Imprimir factura ${factura.numeroCompleto}`,
      ancho: 'sm',
      cuerpo: h`
        <div class="paso-impresion">
          ${factura.vuelto ? h`<div class="vuelto"><span>Vuelto a entregar</span><b>${dinero(factura.vuelto)}</b></div>` : ''}
          <div class="formulario-talonario"><span>Formulario del talonario</span><b>No. ${factura.numero}</b></div>
          <ol>
            <li>Coloque el formulario <b>No. ${factura.numero}</b> en la impresora, alineado en el tractor de arrastre.</li>
            <li>Presione <b>Imprimir</b>.</li>
          </ol>
        </div>`,
      botones: [
        {
          texto: 'Vista previa', izquierda: true,
          accion: () => {
            vistaPrevia(rutaVista(factura.id), `Vista previa · factura ${factura.numeroCompleto}`);
            return false;
          }
        },
        { texto: 'Imprimir más tarde' },
        {
          texto: 'Imprimir  (Enter)', clase: 'btn-primario', primario: true,
          accion: async () => {
            await imprimir(rutaFactura(factura.id));
            impreso = true;
            aviso(`Factura ${factura.numeroCompleto} enviada a la impresora.`);
          }
        }
      ],
      alCerrar: () => resolver(impreso)
    });
  });
}
