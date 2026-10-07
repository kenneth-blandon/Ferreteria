// Catálogo de productos: alta/edición, cambio rápido de precio, ajustes de existencias,
// categorías e importación/exportación con Excel.
import { api, conParametros } from '../api.js';
import {
  $, h, dinero, numero2, cantidad as formatoCantidad, valorEntrada, aNumero, debounce, aviso, confirmar,
  abrirModal, fechaHora, descargar
} from '../ui.js';
import { actualizarAlertas } from '../app.js';

const UNIDADES = ['UND', 'MTS', 'PIE', 'LBS', 'KG', 'GLN', 'LTS', 'ROLLO', 'CAJA', 'BOLSA', 'PAR', 'JGO', 'QQ'];
const TIPOS_MOVIMIENTO = {
  Inicial: 'Existencia inicial', Entrada: 'Entrada', Salida: 'Salida', Conteo: 'Conteo físico',
  Venta: 'Venta', Anulacion: 'Anulación', Importacion: 'Importación'
};

let raiz;
let filtro;
let categorias = [];
let productos = [];

export async function montar(contenedor, parametros) {
  raiz = contenedor;
  filtro = { texto: '', categoriaId: '', bajoStock: parametros.get('bajoStock') === '1', pagina: 1 };
  raiz.innerHTML = plantilla();
  enlazar();
  await cargarCategorias();
  await cargar();
  $('#filtroTexto', raiz).focus();
  return { teclas };
}

function teclas(tecla) {
  if (tecla === 'F3' || tecla === 'Ctrl+N') { formularioProducto(); return true; }
  if (tecla === 'F1') { $('#filtroTexto', raiz).focus(); return true; }
  return false;
}

function plantilla() {
  return h`
    <div class="encabezado">
      <div><h1>Productos</h1><p>Catálogo, precios y existencias. Haga clic en un precio para cambiarlo.</p></div>
      <div class="acciones">
        <button type="button" class="btn" id="btnCategorias">Categorías</button>
        <button type="button" class="btn" id="btnImportar">Importar Excel / CSV</button>
        <button type="button" class="btn" id="btnExportar">Exportar a Excel</button>
        <button type="button" class="btn btn-primario" id="btnNuevo">+ Nuevo producto <kbd>F3</kbd></button>
        <input type="file" id="archivoImportar" accept=".xlsx,.xlsm,.csv,.txt" hidden>
      </div>
    </div>
    <div class="tarjeta">
      <div class="barra">
        <div class="campo crece"><label>Buscar <kbd>F1</kbd></label><input id="filtroTexto" placeholder="Código, nombre, código de barras o categoría" autocomplete="off"></div>
        <div class="campo"><label>Categoría</label><select id="filtroCategoria"></select></div>
        <label class="check" style="height:36px"><input type="checkbox" id="filtroBajo"> Solo stock bajo</label>
      </div>
      <div class="tabla-contenedor">
        <table class="tabla">
          <thead><tr>
            <th>Código</th><th>Nombre</th><th>Categoría</th><th>Unidad</th>
            <th class="num">Precio</th><th class="num">Stock</th><th class="num">Mínimo</th><th></th>
          </tr></thead>
          <tbody id="cuerpo"></tbody>
        </table>
        <div class="vacio" id="vacio" hidden>No hay productos con estos filtros.</div>
      </div>
      <div class="pie-tabla">
        <span id="resumen"></span>
        <div class="paginacion">
          <button type="button" class="btn btn-sm" id="anterior">‹ Anterior</button>
          <span id="pagina"></span>
          <button type="button" class="btn btn-sm" id="siguiente">Siguiente ›</button>
        </div>
      </div>
    </div>`;
}

function enlazar() {
  const recargar = debounce(() => { filtro.pagina = 1; cargar(); }, 250);
  $('#filtroTexto', raiz).addEventListener('input', (e) => { filtro.texto = e.target.value; recargar(); });
  $('#filtroCategoria', raiz).addEventListener('change', (e) => { filtro.categoriaId = e.target.value; filtro.pagina = 1; cargar(); });
  const bajo = $('#filtroBajo', raiz);
  bajo.checked = filtro.bajoStock;
  bajo.addEventListener('change', () => { filtro.bajoStock = bajo.checked; filtro.pagina = 1; cargar(); });
  $('#anterior', raiz).addEventListener('click', () => { filtro.pagina--; cargar(); });
  $('#siguiente', raiz).addEventListener('click', () => { filtro.pagina++; cargar(); });

  $('#btnNuevo', raiz).addEventListener('click', () => formularioProducto());
  $('#btnCategorias', raiz).addEventListener('click', ventanaCategorias);
  $('#btnExportar', raiz).addEventListener('click', () => descargar('/api/inventario/exportar'));
  $('#btnImportar', raiz).addEventListener('click', ventanaImportar);
  $('#archivoImportar', raiz).addEventListener('change', importar);

  $('#cuerpo', raiz).addEventListener('click', (e) => {
    const fila = e.target.closest('tr[data-id]');
    if (!fila) return;
    const producto = productos.find((p) => p.id === Number(fila.dataset.id));
    const accion = e.target.closest('[data-accion]')?.dataset.accion;
    if (accion === 'editar') formularioProducto(producto);
    else if (accion === 'stock') ventanaAjuste(producto);
    else if (accion === 'movimientos') ventanaMovimientos(producto);
    else if (accion === 'eliminar') eliminar(producto);
    else if (e.target.closest('[data-precio]')) editarPrecio(e.target.closest('td'), producto);
  });
}

async function cargarCategorias() {
  categorias = await api.get('/api/categorias');
  const select = $('#filtroCategoria', raiz);
  select.innerHTML = h`<option value="">Todas</option>${categorias.map((c) => h`<option value="${c.id}">${c.nombre} (${c.totalProductos})</option>`)}`;
  select.value = filtro.categoriaId;
}

async function cargar() {
  try {
    const pagina = await api.get(conParametros('/api/productos', {
      texto: filtro.texto, categoriaId: filtro.categoriaId, bajoStock: filtro.bajoStock, pagina: filtro.pagina, tamano: 50
    }));
    if (!raiz.isConnected) return;
    productos = pagina.elementos;
    filtro.pagina = pagina.numeroPagina;
    pintar(pagina);
  } catch (error) {
    aviso(error.message, 'error');
  }
}

function claseStock(p) {
  if (p.stock <= 0) return 'stock-agotado';
  return p.bajoStock ? 'stock-bajo' : '';
}

function pintar(pagina) {
  $('#cuerpo', raiz).innerHTML = h`${productos.map((p) => h`
    <tr data-id="${p.id}">
      <td class="mono">${p.codigo}${p.codigoBarras ? h`<div class="muy-suave">${p.codigoBarras}</div>` : ''}</td>
      <td>${p.nombre}</td>
      <td class="suave">${p.categoria || '—'}</td>
      <td>${p.unidad}</td>
      <td class="num"><span class="editable" data-precio title="Clic para cambiar el precio">${numero2(p.precio)}</span></td>
      <td class="num ${claseStock(p)}">${formatoCantidad(p.stock)}</td>
      <td class="num suave">${p.stockMinimo > 0 ? formatoCantidad(p.stockMinimo) : '—'}</td>
      <td>
        <div class="acciones-fila">
          <button type="button" class="btn btn-sm" data-accion="stock" title="Entrada, salida o conteo físico">Stock</button>
          <button type="button" class="btn btn-sm" data-accion="editar">Editar</button>
          <button type="button" class="btn-icono" data-accion="movimientos" title="Historial de movimientos">≡</button>
          <button type="button" class="btn-icono peligro" data-accion="eliminar" title="Eliminar">✕</button>
        </div>
      </td>
    </tr>`)}`;
  $('#vacio', raiz).hidden = productos.length > 0;
  $('#resumen', raiz).textContent = `${pagina.total} ${pagina.total === 1 ? 'producto' : 'productos'}`;
  $('#pagina', raiz).textContent = `Página ${pagina.numeroPagina} de ${pagina.totalPaginas}`;
  $('#anterior', raiz).disabled = pagina.numeroPagina <= 1;
  $('#siguiente', raiz).disabled = pagina.numeroPagina >= pagina.totalPaginas;
}

// ---------- Cambio rápido de precio ----------

function editarPrecio(celda, producto) {
  if (celda.querySelector('input')) return;
  const original = celda.innerHTML;
  celda.innerHTML = h`<input class="input-celda num" style="width:110px" inputmode="decimal" value="${valorEntrada(producto.precio)}">`;
  const entrada = celda.querySelector('input');
  entrada.focus();
  entrada.select();

  let terminado = false;
  const terminar = async (guardar) => {
    if (terminado) return;
    terminado = true;
    const precio = aNumero(entrada.value);
    if (!guardar || precio === producto.precio) { celda.innerHTML = original; return; }
    if (!(precio >= 0)) { aviso('Precio no válido.', 'error'); celda.innerHTML = original; return; }
    try {
      const actualizado = await api.patch(`/api/productos/${producto.id}/precio`, { precio });
      Object.assign(producto, actualizado);
      celda.innerHTML = h`<span class="editable" data-precio title="Clic para cambiar el precio">${numero2(actualizado.precio)}</span>`;
      aviso(`Precio de ${producto.nombre}: ${dinero(actualizado.precio)}`);
    } catch (error) {
      celda.innerHTML = original;
      aviso(error.message, 'error');
    }
  };
  entrada.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); terminar(true); }
    if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); terminar(false); }
  });
  entrada.addEventListener('blur', () => terminar(true));
}

// ---------- Alta y edición ----------

function formularioProducto(producto = null) {
  const nuevo = !producto;
  const p = producto || { codigo: '', codigoBarras: '', nombre: '', categoriaId: '', unidad: 'UND', precio: '', stockMinimo: 0 };
  abrirModal({
    titulo: nuevo ? 'Nuevo producto' : `Editar ${p.codigo}`,
    ancho: 'lg',
    cuerpo: h`
      <div class="rejilla">
        <div class="campo"><label>Código</label><input name="codigo" value="${p.codigo}" maxlength="40" placeholder="${nuevo ? 'Automático si se deja vacío' : ''}" autocomplete="off"></div>
        <div class="campo"><label>Código de barras</label><input name="codigoBarras" value="${p.codigoBarras || ''}" maxlength="64" placeholder="Opcional" autocomplete="off"></div>
        <div class="campo doble"><label>Nombre / descripción *</label><input name="nombre" value="${p.nombre}" maxlength="200" autocomplete="off"></div>
        <div class="campo"><label>Categoría</label>
          <select name="categoriaId"><option value="">Sin categoría</option>
            ${categorias.map((c) => h`<option value="${c.id}" ${c.id === p.categoriaId ? h`selected` : ''}>${c.nombre}</option>`)}
          </select></div>
        <div class="campo"><label>Unidad</label><input name="unidad" list="unidadesProducto" value="${p.unidad}" maxlength="12">
          <datalist id="unidadesProducto">${UNIDADES.map((u) => h`<option value="${u}">`)}</datalist></div>
        <div class="campo"><label>Precio de venta *</label><input name="precio" class="num" inputmode="decimal" value="${p.precio === '' ? '' : valorEntrada(p.precio)}" placeholder="0.00"></div>
        <div class="campo"><label>Stock mínimo (alerta)</label><input name="stockMinimo" class="num" inputmode="decimal" value="${valorEntrada(p.stockMinimo, 3)}">
          <span class="ayuda">0 = sin alerta</span></div>
        ${nuevo
          ? h`<div class="campo"><label>Existencia inicial</label><input name="stockInicial" class="num" inputmode="decimal" value="0"></div>`
          : h`<div class="campo"><label>Existencia actual</label><input value="${formatoCantidad(p.stock)} ${p.unidad}" disabled>
              <span class="ayuda">Se cambia con el botón "Stock"</span></div>`}
      </div>`,
    botones: [
      { texto: 'Cancelar' },
      {
        texto: nuevo ? 'Crear producto' : 'Guardar cambios', clase: 'btn-primario', primario: true,
        accion: async (modal) => {
          const campo = (nombre) => modal.cuerpo.querySelector(`[name="${nombre}"]`).value;
          const precio = aNumero(campo('precio'));
          const minimo = campo('stockMinimo').trim() === '' ? 0 : aNumero(campo('stockMinimo'));
          const inicial = nuevo ? (campo('stockInicial').trim() === '' ? 0 : aNumero(campo('stockInicial'))) : 0;
          if (!(precio >= 0)) throw new Error('Indique un precio válido.');
          if (!(minimo >= 0) || !(inicial >= 0)) throw new Error('Revise las cantidades.');
          const datos = {
            codigo: campo('codigo'),
            codigoBarras: campo('codigoBarras'),
            nombre: campo('nombre'),
            categoriaId: campo('categoriaId') ? Number(campo('categoriaId')) : null,
            unidad: campo('unidad'),
            precio,
            stockMinimo: minimo,
            stockInicial: inicial
          };
          const guardado = nuevo ? await api.post('/api/productos', datos) : await api.put(`/api/productos/${p.id}`, datos);
          aviso(`Producto ${guardado.codigo} guardado.`);
          await cargar();
          await cargarCategorias();
          actualizarAlertas();
        }
      }
    ]
  });
}

async function eliminar(producto) {
  const ok = await confirmar(
    `¿Eliminar ${producto.codigo} · ${producto.nombre}? Las facturas anteriores no se modifican.`,
    { titulo: 'Eliminar producto', textoSi: 'Eliminar', peligro: true });
  if (!ok) return;
  try {
    await api.borrar(`/api/productos/${producto.id}`);
    aviso('Producto eliminado.');
    await cargar();
    actualizarAlertas();
  } catch (error) {
    aviso(error.message, 'error');
  }
}

// ---------- Existencias ----------

function ventanaAjuste(producto) {
  let tipo = 'Entrada';
  const modal = abrirModal({
    titulo: `Existencias · ${producto.nombre}`,
    ancho: 'sm',
    cuerpo: h`
      <p style="margin:0 0 10px">Existencia actual: <b>${formatoCantidad(producto.stock)} ${producto.unidad}</b></p>
      <div class="segmentado" id="tipoAjuste" style="margin-bottom:12px">
        <button type="button" data-tipo="Entrada" class="activo">+ Entrada</button>
        <button type="button" data-tipo="Salida">− Salida</button>
        <button type="button" data-tipo="Conteo">= Conteo</button>
      </div>
      <div class="campo"><label id="etqCantidad">Cantidad que entra</label><input id="cantidadAjuste" class="num" inputmode="decimal" autocomplete="off"></div>
      <div class="campo" style="margin-top:10px"><label>Nota (opcional)</label><input id="notaAjuste" maxlength="200" placeholder="Ej. compra a proveedor, producto dañado…" autocomplete="off"></div>
      <p class="suave" id="resultadoAjuste" style="margin:10px 0 0"></p>`,
    botones: [
      { texto: 'Cancelar' },
      {
        texto: 'Aplicar', clase: 'btn-primario', primario: true,
        accion: async (m) => {
          const cantidad = aNumero($('#cantidadAjuste', m.cuerpo).value);
          if (!(cantidad >= 0) || (tipo !== 'Conteo' && !(cantidad > 0))) throw new Error('Indique una cantidad válida.');
          const movimiento = await api.post(`/api/inventario/${producto.id}/ajuste`, {
            tipo, cantidad, nota: $('#notaAjuste', m.cuerpo).value
          });
          aviso(`Nueva existencia de ${producto.nombre}: ${formatoCantidad(movimiento.stockResultante)} ${producto.unidad}`);
          await cargar();
          actualizarAlertas();
        }
      }
    ]
  });

  const cuerpo = modal.cuerpo;
  const etiquetas = { Entrada: 'Cantidad que entra', Salida: 'Cantidad que sale', Conteo: 'Cantidad contada físicamente' };
  const vistaPrevia = () => {
    const cantidad = aNumero($('#cantidadAjuste', cuerpo).value);
    if (isNaN(cantidad)) { $('#resultadoAjuste', cuerpo).textContent = ''; return; }
    const nuevo = tipo === 'Entrada' ? producto.stock + cantidad : tipo === 'Salida' ? producto.stock - cantidad : cantidad;
    $('#resultadoAjuste', cuerpo).textContent = `Quedará en ${formatoCantidad(nuevo)} ${producto.unidad}.`;
  };
  $('#tipoAjuste', cuerpo).addEventListener('click', (e) => {
    const boton = e.target.closest('[data-tipo]');
    if (!boton) return;
    tipo = boton.dataset.tipo;
    cuerpo.querySelectorAll('#tipoAjuste button').forEach((b) => b.classList.toggle('activo', b === boton));
    $('#etqCantidad', cuerpo).textContent = etiquetas[tipo];
    vistaPrevia();
    $('#cantidadAjuste', cuerpo).focus();
  });
  $('#cantidadAjuste', cuerpo).addEventListener('input', vistaPrevia);
}

async function ventanaMovimientos(producto) {
  try {
    const movimientos = await api.get(`/api/inventario/${producto.id}/movimientos?limite=200`);
    abrirModal({
      titulo: `Movimientos · ${producto.codigo} ${producto.nombre}`,
      ancho: 'lg',
      cuerpo: movimientos.length
        ? h`<table class="tabla">
            <thead><tr><th>Fecha</th><th>Tipo</th><th class="num">Cambio</th><th class="num">Queda</th><th>Detalle</th></tr></thead>
            <tbody>${movimientos.map((m) => h`
              <tr><td>${fechaHora(m.fecha)}</td><td>${TIPOS_MOVIMIENTO[m.tipo] || m.tipo}</td>
                <td class="num ${m.cantidad < 0 ? 'peligro-texto' : 'exito-texto'}">${m.cantidad > 0 ? '+' : ''}${formatoCantidad(m.cantidad)}</td>
                <td class="num">${formatoCantidad(m.stockResultante)}</td>
                <td class="suave">${[m.referencia, m.nota].filter(Boolean).join(' · ')}</td></tr>`)}
            </tbody></table>`
        : h`<div class="vacio">Sin movimientos registrados.</div>`,
      botones: [{ texto: 'Cerrar', primario: true }]
    });
  } catch (error) {
    aviso(error.message, 'error');
  }
}

// ---------- Categorías ----------

function ventanaCategorias() {
  const modal = abrirModal({
    titulo: 'Categorías / familias',
    cuerpo: h`
      <div class="acciones" style="margin-bottom:12px" data-enter-propio>
        <input id="nuevaCategoria" placeholder="Nueva categoría (ej. Pinturas)" maxlength="60" style="flex:1" autocomplete="off">
        <button type="button" class="btn btn-primario" id="agregarCategoria">Agregar</button>
      </div>
      <table class="tabla"><tbody id="listaCategorias"></tbody></table>`,
    botones: [{ texto: 'Cerrar' }],
    alCerrar: async () => { await cargarCategorias(); await cargar(); }
  });
  const cuerpo = modal.cuerpo;

  const pintarLista = () => {
    $('#listaCategorias', cuerpo).innerHTML = h`${categorias.map((c) => h`
      <tr data-id="${c.id}">
        <td><input value="${c.nombre}" maxlength="60" data-nombre data-enter-propio></td>
        <td class="num suave" style="width:110px">${c.totalProductos} productos</td>
        <td style="width:150px"><div class="acciones-fila">
          <button type="button" class="btn btn-sm" data-guardar>Renombrar</button>
          <button type="button" class="btn-icono peligro" data-borrar title="Eliminar">✕</button>
        </div></td>
      </tr>`)}`;
  };
  const refrescar = async () => { categorias = await api.get('/api/categorias'); pintarLista(); };

  const agregar = async () => {
    const entrada = $('#nuevaCategoria', cuerpo);
    if (!entrada.value.trim()) return;
    try {
      await api.post('/api/categorias', { nombre: entrada.value });
      entrada.value = '';
      await refrescar();
    } catch (error) { aviso(error.message, 'error'); }
  };
  $('#agregarCategoria', cuerpo).addEventListener('click', agregar);
  $('#nuevaCategoria', cuerpo).addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); agregar(); } });

  $('#listaCategorias', cuerpo).addEventListener('click', async (e) => {
    const fila = e.target.closest('tr[data-id]');
    if (!fila) return;
    const id = Number(fila.dataset.id);
    try {
      if (e.target.closest('[data-guardar]')) {
        await api.put(`/api/categorias/${id}`, { nombre: fila.querySelector('[data-nombre]').value });
        aviso('Categoría renombrada.');
        await refrescar();
      } else if (e.target.closest('[data-borrar]')) {
        const categoria = categorias.find((c) => c.id === id);
        if (await confirmar(`¿Eliminar la categoría ${categoria.nombre}? Sus ${categoria.totalProductos} productos quedarán sin categoría.`,
          { titulo: 'Eliminar categoría', textoSi: 'Eliminar', peligro: true })) {
          await api.borrar(`/api/categorias/${id}`);
          await refrescar();
        }
      }
    } catch (error) { aviso(error.message, 'error'); }
  });
  pintarLista();
  $('#nuevaCategoria', cuerpo).focus();
}

// ---------- Importación ----------

function ventanaImportar() {
  abrirModal({
    titulo: 'Importar catálogo desde Excel o CSV',
    cuerpo: h`
      <p style="margin-top:0">El archivo debe tener en la primera fila los encabezados. Obligatorias:
        <b>Código</b>, <b>Nombre</b> y <b>Precio</b>. Opcionales: Stock, Categoría, Unidad, Stock mínimo, Código de barras.</p>
      <ul class="suave" style="padding-left:18px">
        <li>Si el código ya existe, se actualiza el producto (no se duplica).</li>
        <li>Una celda vacía conserva el valor actual (por ejemplo, la existencia).</li>
        <li>Las categorías nuevas se crean solas.</li>
      </ul>
      <button type="button" class="btn-link" id="bajarPlantilla">Descargar plantilla de ejemplo (.xlsx)</button>`,
    botones: [
      { texto: 'Cancelar' },
      {
        texto: 'Elegir archivo…', clase: 'btn-primario', primario: true,
        accion: () => { $('#archivoImportar', raiz).click(); }
      }
    ]
  }).cuerpo.querySelector('#bajarPlantilla').addEventListener('click', () => descargar('/api/inventario/plantilla'));
}

async function importar(e) {
  const archivo = e.target.files[0];
  e.target.value = '';
  if (!archivo) return;
  aviso(`Importando ${archivo.name}…`, 'aviso', 2000);
  try {
    const r = await api.subir('/api/inventario/importar', archivo);
    abrirModal({
      titulo: 'Resultado de la importación',
      ancho: 'lg',
      cuerpo: h`
        <div class="indicadores">
          <div class="tarjeta indicador"><span>Filas leídas</span><b>${r.filasLeidas}</b></div>
          <div class="tarjeta indicador"><span>Productos nuevos</span><b class="exito-texto">${r.insertados}</b></div>
          <div class="tarjeta indicador"><span>Actualizados</span><b>${r.actualizados}</b></div>
          <div class="tarjeta indicador"><span>Con errores</span><b class="${r.errores.length ? 'peligro-texto' : ''}">${r.errores.length}</b></div>
        </div>
        ${r.errores.length
          ? h`<p class="suave">Estas filas no se importaron. Corríjalas en el archivo y vuelva a importarlo (las demás no se duplicarán):</p>
              <table class="tabla"><thead><tr><th style="width:70px">Fila</th><th>Motivo</th></tr></thead>
              <tbody>${r.errores.map((x) => h`<tr><td>${x.fila}</td><td>${x.mensaje}</td></tr>`)}</tbody></table>`
          : h`<div class="info-caja">Todas las filas se importaron correctamente.</div>`}`,
      botones: [{ texto: 'Cerrar', primario: true }]
    });
    await cargarCategorias();
    await cargar();
    actualizarAlertas();
  } catch (error) {
    aviso(error.message, 'error', 8000);
  }
}
