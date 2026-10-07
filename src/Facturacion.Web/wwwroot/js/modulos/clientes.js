// Clientes frecuentes (opcional: por defecto se factura a Consumidor Final).
import { api, conParametros } from '../api.js';
import { $, h, debounce, aviso, confirmar, abrirModal, fecha } from '../ui.js';

let raiz;
let clientes = [];
let texto = '';

export async function montar(contenedor) {
  raiz = contenedor;
  raiz.innerHTML = h`
    <div class="encabezado">
      <div><h1>Clientes</h1><p>Clientes frecuentes para cargar sus datos en la factura con pocas teclas.</p></div>
      <button type="button" class="btn btn-primario" id="btnNuevo">+ Nuevo cliente <kbd>F3</kbd></button>
    </div>
    <div class="tarjeta">
      <div class="barra"><div class="campo crece"><label>Buscar <kbd>F1</kbd></label><input id="buscar" placeholder="Nombre, RUC o teléfono" autocomplete="off"></div></div>
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead><tr><th>Nombre</th><th>RUC / Cédula</th><th>Dirección</th><th>Teléfono</th><th>Registrado</th><th></th></tr></thead>
          <tbody id="cuerpo"></tbody>
        </table>
        <div class="vacio" id="vacio" hidden>No hay clientes registrados con ese criterio.</div>
      </div>
    </div>`;

  const recargar = debounce(cargar, 250);
  $('#buscar', raiz).addEventListener('input', (e) => { texto = e.target.value; recargar(); });
  $('#btnNuevo', raiz).addEventListener('click', () => formulario());
  $('#cuerpo', raiz).addEventListener('click', (e) => {
    const fila = e.target.closest('tr[data-id]');
    const accion = e.target.closest('[data-accion]')?.dataset.accion;
    if (!fila || !accion) return;
    const cliente = clientes.find((c) => c.id === Number(fila.dataset.id));
    if (accion === 'editar') formulario(cliente);
    else if (accion === 'eliminar') eliminar(cliente);
  });

  await cargar();
  $('#buscar', raiz).focus();
  return {
    teclas(tecla) {
      if (tecla === 'F3' || tecla === 'Ctrl+N') { formulario(); return true; }
      if (tecla === 'F1') { $('#buscar', raiz).focus(); return true; }
      return false;
    }
  };
}

async function cargar() {
  try {
    clientes = await api.get(conParametros('/api/clientes', { texto, limite: 200 }));
    if (!raiz.isConnected) return;
    $('#cuerpo', raiz).innerHTML = h`${clientes.map((c) => h`
      <tr data-id="${c.id}">
        <td><b>${c.nombre}</b></td><td class="mono">${c.ruc || '—'}</td><td>${c.direccion || '—'}</td>
        <td>${c.telefono || '—'}</td><td class="suave">${fecha(c.creado)}</td>
        <td><div class="acciones-fila">
          <button type="button" class="btn btn-sm" data-accion="editar">Editar</button>
          <button type="button" class="btn-icono peligro" data-accion="eliminar" title="Eliminar">✕</button>
        </div></td>
      </tr>`)}`;
    $('#vacio', raiz).hidden = clientes.length > 0;
  } catch (error) {
    aviso(error.message, 'error');
  }
}

function formulario(cliente = null) {
  const c = cliente || { nombre: '', ruc: '', direccion: '', telefono: '' };
  abrirModal({
    titulo: cliente ? 'Editar cliente' : 'Nuevo cliente',
    cuerpo: h`
      <div class="rejilla">
        <div class="campo completo"><label>Nombre o razón social *</label><input name="nombre" value="${c.nombre}" maxlength="120" autocomplete="off"></div>
        <div class="campo"><label>RUC / Cédula</label><input name="ruc" value="${c.ruc || ''}" maxlength="30" autocomplete="off"></div>
        <div class="campo"><label>Teléfono</label><input name="telefono" value="${c.telefono || ''}" maxlength="40" autocomplete="off"></div>
        <div class="campo completo"><label>Dirección</label><input name="direccion" value="${c.direccion || ''}" maxlength="200" autocomplete="off"></div>
      </div>`,
    botones: [
      { texto: 'Cancelar' },
      {
        texto: 'Guardar', clase: 'btn-primario', primario: true,
        accion: async (modal) => {
          const valor = (nombre) => modal.cuerpo.querySelector(`[name="${nombre}"]`).value;
          const datos = { nombre: valor('nombre'), ruc: valor('ruc'), direccion: valor('direccion'), telefono: valor('telefono') };
          if (cliente) await api.put(`/api/clientes/${cliente.id}`, datos);
          else await api.post('/api/clientes', datos);
          aviso('Cliente guardado.');
          await cargar();
        }
      }
    ]
  });
}

async function eliminar(cliente) {
  if (!(await confirmar(`¿Eliminar a ${cliente.nombre}? Sus facturas anteriores se conservan.`,
    { titulo: 'Eliminar cliente', textoSi: 'Eliminar', peligro: true }))) return;
  try {
    await api.borrar(`/api/clientes/${cliente.id}`);
    aviso('Cliente eliminado.');
    await cargar();
  } catch (error) {
    aviso(error.message, 'error');
  }
}
