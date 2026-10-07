namespace Facturacion.Core.Comun;

/// <summary>
/// Error de validación o de regla de negocio. El mensaje se muestra tal cual al usuario,
/// por eso debe estar redactado en lenguaje sencillo.
/// </summary>
public class ReglaNegocioException(string mensaje) : Exception(mensaje);

/// <summary>El registro solicitado no existe.</summary>
public class NoEncontradoException(string mensaje) : Exception(mensaje);
