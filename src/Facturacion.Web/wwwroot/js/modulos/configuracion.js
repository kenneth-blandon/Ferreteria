// Configuración: datos del negocio, numeración e IVA, impresión (con calibración) y respaldos.
import { api } from '../api.js';
import { $, h, aviso, confirmar, descargar, leerFormulario } from '../ui.js';
import { obtenerConfiguracion } from '../config.js';
import { imprimir, vistaPrevia, esEscritorio } from '../impresion.js';

let raiz;

export async function montar(contenedor) {
  raiz = contenedor;
  const [ajustes, impresoras, sistema] = await Promise.all([
    obtenerConfiguracion(true),
    api.get('/api/impresoras'),
    api.get('/api/sistema')
  ]);
  const { negocio: n, facturacion: f, impresion: i } = ajustes;

  raiz.innerHTML = h`
    <div class="encabezado"><div><h1>Configuración</h1><p>Cada sección se guarda por separado.</p></div></div>
    <div class="columnas">
      <div>
        <form class="tarjeta seccion" id="formNegocio">
          <h2>Datos del negocio</h2>
          <div class="rejilla">
            <div class="campo"><label>Nombre comercial *</label><input name="nombreComercial" value="${n.nombreComercial}" maxlength="80"></div>
            <div class="campo"><label>Giro / actividad</label><input name="giro" value="${n.giro}" maxlength="80"></div>
            <div class="campo"><label>Propietario</label><input name="propietario" value="${n.propietario}" maxlength="80"></div>
            <div class="campo"><label>RUC</label><input name="ruc" value="${n.ruc}" maxlength="30"></div>
            <div class="campo completo"><label>Dirección</label><input name="direccion" value="${n.direccion}" maxlength="160"></div>
            <div class="campo"><label>Teléfonos</label><input name="telefono" value="${n.telefono}" maxlength="60"></div>
            <div class="campo"><label>Correo</label><input name="email" value="${n.email}" maxlength="80"></div>
            <div class="campo completo"><label>Mensaje al pie del ticket</label><input name="mensajePie" value="${n.mensajePie}" maxlength="120"></div>
            <div class="campo completo"><label>Condiciones impresas en la factura (una por línea)</label>
              <textarea name="condiciones" rows="5" maxlength="2000">${n.condiciones}</textarea></div>
          </div>
          <div class="pie-acciones"><button type="submit" class="btn btn-primario">Guardar datos del negocio</button></div>
        </form>

        <form class="tarjeta seccion" id="formFacturacion">
          <h2>Numeración e impuestos</h2>
          <div class="info-caja" style="margin-bottom:10px">El <b>siguiente número</b> debe coincidir con el próximo formulario del talonario físico.</div>
          <div class="rejilla">
            <div class="campo"><label>Serie</label><input name="serie" value="${f.serie}" maxlength="5"></div>
            <div class="campo"><label>Tipo de factura</label><input name="tipoFactura" value="${f.tipoFactura}" maxlength="20"></div>
            <div class="campo"><label>Siguiente número</label><input name="siguienteNumero" type="number" min="1" value="${f.siguienteNumero}"></div>
            <div class="campo"><label>Rango autorizado desde</label><input name="rangoDesde" type="number" min="1" value="${f.rangoDesde ?? ''}" placeholder="Opcional"></div>
            <div class="campo"><label>Rango autorizado hasta</label><input name="rangoHasta" type="number" min="1" value="${f.rangoHasta ?? ''}" placeholder="Opcional"></div>
          </div>
          <div style="display:flex;flex-direction:column;gap:8px;margin-top:12px">
            <label class="check"><input type="checkbox" name="ivaHabilitado" ${f.ivaHabilitado ? h`checked` : ''}> Cobrar IVA (desactivado para contribuyentes de Cuota Fija)</label>
            <div class="rejilla">
              <div class="campo"><label>Porcentaje de IVA</label><input name="ivaPorcentaje" type="number" min="0" max="100" step="0.01" value="${f.ivaPorcentaje}"></div>
            </div>
            <label class="check"><input type="checkbox" name="preciosIncluyenIva" ${f.preciosIncluyenIva ? h`checked` : ''}> Los precios del catálogo ya incluyen IVA</label>
            <label class="check"><input type="checkbox" name="permitirStockNegativo" ${f.permitirStockNegativo ? h`checked` : ''}> Permitir facturar aunque el sistema no tenga existencias</label>
          </div>
          <div class="pie-acciones"><button type="submit" class="btn btn-primario">Guardar numeración e impuestos</button></div>
        </form>
      </div>

      <div>
        <form class="tarjeta seccion" id="formImpresion">
          <h2>Impresión</h2>
          <div class="rejilla">
            <div class="campo"><label>Formato predeterminado</label>
              <select name="formato">
                ${[['Carta', 'Hoja carta (inyección / láser)'], ['Ticket', 'Ticket (térmica)'], ['Preimpreso', 'Talonario preimpreso (matriz de punto)']]
                  .map(([v, t]) => h`<option value="${v}" ${i.formato === v ? h`selected` : ''}>${t}</option>`)}
              </select></div>
            <div class="campo"><label>Impresora</label>
              ${impresoras.impresoras.length
                ? h`<select name="impresora"><option value="">Predeterminada de Windows${impresoras.predeterminada ? ` (${impresoras.predeterminada})` : ''}</option>
                    ${impresoras.impresoras.map((nombre) => h`<option value="${nombre}" ${i.impresora === nombre ? h`selected` : ''}>${nombre}</option>`)}</select>`
                : h`<input name="impresora" value="${i.impresora || ''}" placeholder="Predeterminada">`}
            </div>
            <div class="campo"><label>Copias</label><input name="copias" type="number" min="1" max="5" value="${i.copias}"></div>
            <div class="campo"><label>Ancho del rollo térmico</label>
              <select name="anchoTicketMm">${[80, 58].map((a) => h`<option value="${a}" ${i.anchoTicketMm === a ? h`selected` : ''}>${a} mm</option>`)}</select></div>
          </div>
          <div style="display:flex;flex-direction:column;gap:8px;margin-top:12px">
            <label class="check"><input type="checkbox" name="imprimirAlCobrar" ${i.imprimirAlCobrar ? h`checked` : ''}> Imprimir automáticamente al cobrar</label>
            <label class="check"><input type="checkbox" name="mostrarDialogo" ${i.mostrarDialogo ? h`checked` : ''}> Mostrar el cuadro de impresión (en lugar de imprimir directo)</label>
          </div>
          ${esEscritorio ? '' : h`<div class="aviso-caja" style="margin-top:10px">Modo navegador: se usa el cuadro de impresión del navegador. Elija márgenes "Ninguno" y escala 100%.</div>`}

          <h3 style="margin-top:16px">Calibración del talonario preimpreso</h3>
          <p class="suave" style="margin:4px 0 10px">Imprima la hoja de prueba en un formulario (o en papel en blanco y sobrepóngalo a contraluz).
            Si los datos salen corridos, ajuste el desplazamiento: X positivo mueve a la derecha, Y positivo mueve hacia abajo.</p>
          <div class="rejilla">
            <div class="campo"><label>Desplazamiento X (mm)</label><input name="desplazamientoXMm" type="number" step="0.5" value="${i.desplazamientoXMm}"></div>
            <div class="campo"><label>Desplazamiento Y (mm)</label><input name="desplazamientoYMm" type="number" step="0.5" value="${i.desplazamientoYMm}"></div>
          </div>
          <details style="margin-top:8px">
            <summary>Posiciones avanzadas (mm)</summary>
            <p class="suave" style="margin:4px 0">Posición de cada campo desde la esquina superior izquierda del formulario.</p>
            <textarea id="plantillaJson" rows="14" class="mono" spellcheck="false">${JSON.stringify(i.plantilla, null, 2)}</textarea>
            <button type="button" class="btn btn-sm" id="restablecerPlantilla" style="margin-top:6px">Restablecer posiciones originales</button>
          </details>
          <div class="pie-acciones">
            <button type="button" class="btn" id="vistaCalibracion">Ver hoja de prueba</button>
            <button type="button" class="btn" id="imprimirCalibracion">Imprimir hoja de prueba</button>
            <button type="submit" class="btn btn-primario">Guardar impresión</button>
          </div>
        </form>

        <div class="tarjeta seccion">
          <h2>Respaldos</h2>
          <p class="suave" style="margin-top:0">El sistema guarda automáticamente una copia diaria (últimos 30 días).
            Lleve además una copia a una memoria USB o a la nube con regularidad.</p>
          <dl class="lista-datos">
            <dt>Base de datos</dt><dd class="mono">${sistema.archivoBaseDatos}</dd>
            <dt>Copias diarias</dt><dd class="mono">${sistema.carpetaRespaldos}</dd>
            <dt>Versión</dt><dd>${sistema.version} · ${sistema.host}</dd>
          </dl>
          <div class="pie-acciones">
            <input type="file" id="archivoRestaurar" accept=".db" hidden>
            <button type="button" class="btn" id="restaurar">Restaurar desde respaldo…</button>
            <button type="button" class="btn btn-primario" id="respaldar">Descargar respaldo ahora</button>
          </div>
        </div>
      </div>
    </div>`;

  enlazar();
}

function numeroONulo(valor) {
  const texto = String(valor ?? '').trim();
  return texto === '' ? null : Number(texto);
}

async function guardar(url, datos, mensaje) {
  try {
    await api.put(url, datos);
    await obtenerConfiguracion(true);
    aviso(mensaje);
    return true;
  } catch (error) {
    aviso(error.message, 'error', 7000);
    return false;
  }
}

function enlazar() {
  $('#formNegocio', raiz).addEventListener('submit', async (e) => {
    e.preventDefault();
    if (await guardar('/api/configuracion/negocio', leerFormulario(e.target), 'Datos del negocio guardados.')) {
      const { negocio } = await obtenerConfiguracion();
      $('#marcaNombre').textContent = negocio.nombreComercial;
      $('#marcaGiro').textContent = negocio.giro || 'Ferretería';
    }
  });

  $('#formFacturacion', raiz).addEventListener('submit', async (e) => {
    e.preventDefault();
    const d = leerFormulario(e.target);
    await guardar('/api/configuracion/facturacion', {
      ...d,
      siguienteNumero: numeroONulo(d.siguienteNumero),
      rangoDesde: numeroONulo(d.rangoDesde),
      rangoHasta: numeroONulo(d.rangoHasta),
      ivaPorcentaje: numeroONulo(d.ivaPorcentaje) ?? 0
    }, 'Numeración e impuestos guardados.');
  });

  $('#formImpresion', raiz).addEventListener('submit', async (e) => {
    e.preventDefault();
    await guardarImpresion();
  });

  $('#restablecerPlantilla', raiz).addEventListener('click', async () => {
    const plantilla = await api.get('/api/configuracion/plantilla-preimpresa');
    $('#plantillaJson', raiz).value = JSON.stringify(plantilla, null, 2);
    aviso('Posiciones restablecidas. Pulse "Guardar impresión" para aplicarlas.', 'aviso');
  });

  // La hoja de prueba usa lo guardado: se guarda antes de mostrarla.
  $('#vistaCalibracion', raiz).addEventListener('click', async () => {
    if (await guardarImpresion()) vistaPrevia('/imprimir/calibracion?cuadricula=true', 'Hoja de prueba del preimpreso');
  });
  $('#imprimirCalibracion', raiz).addEventListener('click', async () => {
    if (!(await guardarImpresion())) return;
    try {
      await imprimir('/imprimir/calibracion?cuadricula=false');
      aviso('Hoja de prueba enviada a la impresora.');
    } catch (error) {
      aviso(error.message, 'error', 7000);
    }
  });

  $('#respaldar', raiz).addEventListener('click', () => descargar('/api/respaldo'));
  $('#restaurar', raiz).addEventListener('click', () => $('#archivoRestaurar', raiz).click());
  $('#archivoRestaurar', raiz).addEventListener('change', async (e) => {
    const archivo = e.target.files[0];
    e.target.value = '';
    if (!archivo) return;
    const ok = await confirmar(
      `Se reemplazarán TODOS los datos actuales (productos, facturas, configuración) por los del archivo "${archivo.name}". ` +
      'Antes se guardará una copia de seguridad del estado actual. ¿Continuar?',
      { titulo: 'Restaurar respaldo', textoSi: 'Restaurar', peligro: true });
    if (!ok) return;
    try {
      const r = await api.subir('/api/respaldo/restaurar', archivo);
      aviso(r.mensaje, 'ok', 6000);
      setTimeout(() => location.reload(), 1200);
    } catch (error) {
      aviso(error.message, 'error', 8000);
    }
  });
}

async function guardarImpresion() {
  const formulario = $('#formImpresion', raiz);
  const d = leerFormulario(formulario);
  let plantilla;
  try {
    plantilla = JSON.parse($('#plantillaJson', raiz).value);
  } catch {
    aviso('Las posiciones avanzadas no tienen un formato válido.', 'error', 6000);
    return false;
  }
  return guardar('/api/configuracion/impresion', {
    formato: d.formato,
    impresora: d.impresora,
    imprimirAlCobrar: d.imprimirAlCobrar,
    mostrarDialogo: d.mostrarDialogo,
    copias: numeroONulo(d.copias) ?? 1,
    anchoTicketMm: numeroONulo(d.anchoTicketMm) ?? 80,
    desplazamientoXMm: numeroONulo(d.desplazamientoXMm) ?? 0,
    desplazamientoYMm: numeroONulo(d.desplazamientoYMm) ?? 0,
    plantilla
  }, 'Configuración de impresión guardada.');
}
