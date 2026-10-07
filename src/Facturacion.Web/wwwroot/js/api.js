// Cliente HTTP para la API local. Los errores traen el mensaje que envía el servidor.

export class ErrorApi extends Error {
  constructor(mensaje, estado) {
    super(mensaje);
    this.estado = estado;
  }
}

export async function solicitar(metodo, url, datos) {
  // X-Facturacion: el servidor rechaza cambios que no vengan de esta aplicación.
  const opciones = { method: metodo, headers: { Accept: 'application/json', 'X-Facturacion': '1' } };
  if (datos instanceof FormData) {
    opciones.body = datos;
  } else if (datos !== undefined) {
    opciones.headers['Content-Type'] = 'application/json';
    opciones.body = JSON.stringify(datos);
  }

  let respuesta;
  try {
    respuesta = await fetch(url, opciones);
  } catch {
    throw new ErrorApi('No hay conexión con el servidor local. Cierre y vuelva a abrir la aplicación.', 0);
  }

  if (respuesta.status === 204) return null;
  const esJson = (respuesta.headers.get('content-type') || '').includes('json');
  const cuerpo = esJson ? await respuesta.json() : await respuesta.text();
  if (!respuesta.ok) {
    const mensaje = (esJson && cuerpo?.error) || `Error ${respuesta.status} al comunicarse con el servidor.`;
    throw new ErrorApi(mensaje, respuesta.status);
  }
  return cuerpo;
}

export const api = {
  get: (url) => solicitar('GET', url),
  post: (url, datos) => solicitar('POST', url, datos ?? {}),
  put: (url, datos) => solicitar('PUT', url, datos),
  patch: (url, datos) => solicitar('PATCH', url, datos),
  borrar: (url) => solicitar('DELETE', url),
  subir: (url, archivo, campo = 'archivo') => {
    const formulario = new FormData();
    formulario.append(campo, archivo);
    return solicitar('POST', url, formulario);
  }
};

/** Arma una URL con parámetros de consulta, omitiendo los vacíos. */
export function conParametros(ruta, parametros) {
  const consulta = new URLSearchParams();
  for (const [clave, valor] of Object.entries(parametros)) {
    if (valor !== undefined && valor !== null && valor !== '' && valor !== false) consulta.set(clave, valor);
  }
  const texto = consulta.toString();
  return texto ? `${ruta}?${texto}` : ruta;
}
