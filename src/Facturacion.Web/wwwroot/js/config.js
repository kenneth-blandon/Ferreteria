// Configuración compartida entre módulos (se pide una vez y se refresca al guardar).
import { api } from './api.js';

let cache = null;

export async function obtenerConfiguracion(refrescar = false) {
  if (!cache || refrescar) cache = await api.get('/api/configuracion');
  return cache;
}

export function olvidarConfiguracion() {
  cache = null;
}
