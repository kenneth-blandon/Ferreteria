namespace Facturacion.Core.Configuracion;

/// <summary>Almacén clave/valor de la configuración.</summary>
public interface IConfiguracionRepositorio
{
    string? Leer(string clave);
    void Escribir(string clave, string valor);
}

/// <summary>Claves usadas en la tabla Configuracion.</summary>
public static class ClavesConfiguracion
{
    public const string Negocio = "negocio";
    public const string Facturacion = "facturacion";
    public const string Impresion = "impresion";
    /// <summary>Se guarda aparte porque la incrementa cada factura emitida.</summary>
    public const string SiguienteNumero = "facturacion.siguiente_numero";
}
