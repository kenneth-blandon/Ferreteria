// Validación de teléfono, cédula y RUC de Nicaragua en la pantalla (aviso inmediato).
// Son las mismas reglas del servidor (Core/Comun/Identificacion.cs), que es quien decide al guardar.

const LETRAS_CONTROL = 'ABCDEFGHJKLMNPQRSTUVWXY';
export const EJEMPLO_TELEFONO = '89704920';
export const FORMATO_CEDULA = '000-000000-0000A';
export const EJEMPLO_RUC = 'J0310000007669';
const PATRON_CEDULA = /^(\d{3})-?(\d{6})-?(\d{4})([A-Z])$/;

const valido = (valor) => ({ valido: true, valor, mensaje: '' });
const invalido = (mensaje) => ({ valido: false, valor: null, mensaje });

/** 8 dígitos (acepta guiones, espacios y +505). */
export function telefono(texto) {
  const t = String(texto ?? '').trim();
  if (!t) return valido('');
  if (!/^[\d\s().+-]+$/.test(t)) return invalido(`El teléfono solo puede tener números (ej. ${EJEMPLO_TELEFONO}).`);
  let digitos = t.replace(/\D/g, '');
  if (digitos.length === 11 && digitos.startsWith('505')) digitos = digitos.slice(3);
  return digitos.length === 8 ? valido(digitos) : invalido(`El teléfono debe tener 8 dígitos (ej. ${EJEMPLO_TELEFONO}).`);
}

/** Uno o varios teléfonos separados por "/" (datos del negocio). */
export function telefonos(texto) {
  const partes = String(texto ?? '').split(/[/,;]/).map((p) => p.trim()).filter(Boolean);
  const normalizados = [];
  for (const parte of partes) {
    const resultado = telefono(parte);
    if (!resultado.valido) return invalido(`Revise "${parte}": cada teléfono debe tener 8 dígitos (ej. ${EJEMPLO_TELEFONO}).`);
    normalizados.push(resultado.valor);
  }
  return valido(normalizados.join(' / '));
}

function fechaValida(ddmmaa) {
  const dia = Number(ddmmaa.slice(0, 2));
  const mes = Number(ddmmaa.slice(2, 4));
  const anio = Number(ddmmaa.slice(4));
  if (mes < 1 || mes > 12 || dia < 1) return false;
  const diasDelMes = (a) => new Date(a, mes, 0).getDate();
  return dia <= diasDelMes(1900 + anio) || dia <= diasDelMes(2000 + anio);
}

function revisarCedula(treceDigitos, letra, nombre) {
  const fecha = treceDigitos.slice(3, 9);
  if (!fechaValida(fecha)) {
    return `Revise ${nombre}: la fecha de nacimiento ${fecha.slice(0, 2)}/${fecha.slice(2, 4)}/${fecha.slice(4)} no existe.`;
  }
  if (LETRAS_CONTROL[Number(treceDigitos) % 23] !== letra) return `Revise ${nombre}: la letra final no corresponde a los números.`;
  return null;
}

/** Cédula 000-000000-0000A (con o sin guiones). */
export function cedula(texto) {
  const t = String(texto ?? '').toUpperCase().replace(/\s/g, '');
  if (!t) return valido('');
  const m = PATRON_CEDULA.exec(t);
  if (!m) return invalido(`La cédula no tiene el formato correcto (${FORMATO_CEDULA}).`);
  const problema = revisarCedula(m[1] + m[2] + m[3], m[4], 'la cédula');
  return problema ? invalido(problema) : valido(`${m[1]}-${m[2]}-${m[3]}${m[4]}`);
}

/** RUC de 14 caracteres (aviso DGI): 13 dígitos + letra, o J/N/R/E + 13 dígitos. */
export function ruc(texto) {
  const t = String(texto ?? '').toUpperCase().replace(/[\s-]/g, '');
  if (!t) return valido('');
  if (t.length !== 14) return invalido(`El RUC debe tener 14 caracteres (ej. ${EJEMPLO_RUC}).`);
  if (/^[JNRE]\d{13}$/.test(t)) return valido(t);
  if (/^\d{13}[A-Z]$/.test(t)) {
    if (t.startsWith('0000')) return invalido('Los RUC que inician con cuatro ceros no son válidos (aviso DGI 072/05-2022).');
    const problema = revisarCedula(t.slice(0, 13), t[13], 'el RUC');
    return problema ? invalido(problema) : valido(t);
  }
  return invalido(`El RUC no es válido. Debe ser 13 dígitos y una letra o J, N, R o E seguida de 13 dígitos (ej. ${EJEMPLO_RUC}).`);
}

/** Campo "RUC / Cédula": con guiones es cédula; sin guiones, RUC de 14 caracteres. */
export function documento(texto) {
  const t = String(texto ?? '').toUpperCase().replace(/\s/g, '');
  if (!t) return valido('');
  if (t.includes('-') && PATRON_CEDULA.test(t)) return cedula(t);
  if (t.length === 14 && !t.includes('-')) return ruc(t);
  return invalido(`RUC o cédula no válido. Cédula: ${FORMATO_CEDULA} · RUC: 14 caracteres (ej. ${EJEMPLO_RUC}).`);
}

/**
 * Revisa un campo al salir de él: si es inválido lo marca en rojo y muestra el motivo debajo;
 * si es válido lo deja con el formato correcto. Devuelve una función para revisarlo a demanda.
 */
export function validarAlSalir(entrada, validador) {
  const mensaje = document.createElement('div');
  mensaje.className = 'mensaje-campo';
  mensaje.hidden = true;
  entrada.closest('.campo')?.append(mensaje);

  const revisar = () => {
    const resultado = validador(entrada.value);
    entrada.classList.toggle('invalido', !resultado.valido);
    mensaje.hidden = resultado.valido;
    mensaje.textContent = resultado.mensaje;
    if (resultado.valido && entrada.value !== resultado.valor) {
      entrada.value = resultado.valor;
      entrada.dispatchEvent(new Event('input', { bubbles: true }));
    }
    return resultado.valido;
  };
  entrada.addEventListener('blur', revisar);
  entrada.addEventListener('input', () => {
    if (mensaje.hidden) return;
    mensaje.hidden = true;
    entrada.classList.remove('invalido');
  });
  return revisar;
}
