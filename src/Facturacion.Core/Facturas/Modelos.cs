using Facturacion.Core.Comun;

namespace Facturacion.Core.Facturas;

public enum MetodoPago { Efectivo, Tarjeta, Transferencia }

public enum EstadoFactura { Emitida, Anulada }

/// <summary>
/// Factura emitida. Guarda una copia de los datos del cliente y de los productos
/// tal como estaban al momento de facturar (documento fiscal).
/// </summary>
public sealed class Factura
{
    public const string ConsumidorFinal = "CONSUMIDOR FINAL";

    public long Id { get; set; }
    public string Serie { get; set; } = "";
    public long Numero { get; set; }
    public string NumeroCompleto => $"{Serie}-{Numero}";
    public DateTime Fecha { get; set; }

    public long? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = ConsumidorFinal;
    public string? ClienteRuc { get; set; }
    public string? ClienteDireccion { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    public MetodoPago MetodoPago { get; set; }
    public decimal? MontoRecibido { get; set; }
    public decimal? Vuelto { get; set; }
    public string? Referencia { get; set; }

    public EstadoFactura Estado { get; set; } = EstadoFactura.Emitida;
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    /// <summary>Fecha de la última modificación (null si nunca se modificó).</summary>
    public DateTime? FechaModificacion { get; set; }
    public bool Modificada => FechaModificacion.HasValue;

    public List<LineaFactura> Lineas { get; set; } = [];

    /// <summary>Historial de modificaciones (solo al consultar una factura).</summary>
    public List<ModificacionFactura> Modificaciones { get; set; } = [];
}

/// <summary>Constancia de una modificación: quién, por qué y qué cambió.</summary>
public sealed class ModificacionFactura
{
    public long Id { get; set; }
    public long FacturaId { get; set; }
    public DateTime Fecha { get; set; }
    public string Empleado { get; set; } = "";
    public string Motivo { get; set; } = "";
    public List<string> Cambios { get; set; } = [];
    public decimal TotalAnterior { get; set; }
    public decimal TotalNuevo { get; set; }
}

/// <summary>Datos corregidos de una factura emitida más la constancia obligatoria.</summary>
public sealed record SolicitudModificacion(
    ClienteSolicitud? Cliente,
    IReadOnlyList<LineaSolicitud>? Lineas,
    MetodoPago MetodoPago,
    decimal? MontoRecibido,
    string? Referencia,
    string? Motivo,
    string? Empleado)
{
    public SolicitudFactura ComoFactura() => new(Cliente, Lineas, MetodoPago, MontoRecibido, Referencia);
}

public sealed class LineaFactura
{
    public long Id { get; set; }
    public long FacturaId { get; set; }
    /// <summary>Null en artículos libres (no registrados en el catálogo).</summary>
    public long? ProductoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Unidad { get; set; } = "UND";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
}

/// <summary>Lo que envía la pantalla de facturación al cobrar.</summary>
public sealed record SolicitudFactura(
    ClienteSolicitud? Cliente,
    IReadOnlyList<LineaSolicitud>? Lineas,
    MetodoPago MetodoPago,
    decimal? MontoRecibido,
    string? Referencia);

public sealed record ClienteSolicitud(long? Id, string? Nombre, string? Ruc, string? Direccion, bool Guardar = false);

/// <summary>Con ProductoId: producto del catálogo. Sin ProductoId: artículo libre (requiere descripción).</summary>
public sealed record LineaSolicitud(long? ProductoId, string? Descripcion, string? Unidad, decimal Cantidad, decimal Precio);

public sealed record FiltroFacturas(DateOnly Desde, DateOnly Hasta, string? Texto = null, EstadoFactura? Estado = null);

public sealed record TotalesFactura(decimal Subtotal, decimal Iva, decimal Total);

public sealed record ConfiguracionIva(bool Habilitado, decimal Porcentaje, bool PreciosIncluyenIva)
{
    public static readonly ConfiguracionIva SinIva = new(false, 0, false);
}

/// <summary>Información del próximo número para mostrar en pantalla.</summary>
public sealed record ProximoNumero(string Serie, long Numero, long? RangoHasta, long? Restantes, string? Advertencia);

/// <summary>Serie y rango autorizado vigentes al emitir.</summary>
public sealed record ReglasNumeracion(string Serie, long? RangoDesde, long? RangoHasta)
{
    /// <summary>Avisar cuando queden pocos formularios en el talonario.</summary>
    public const int UmbralAviso = 50;

    public void Validar(long numero)
    {
        if (RangoDesde is { } desde && numero < desde)
            throw new ReglaNegocioException(
                $"El número {Serie}-{numero} es menor al rango autorizado ({desde}–{RangoHasta}). Revise Configuración > Facturación.");
        if (RangoHasta is { } hasta && numero > hasta)
            throw new ReglaNegocioException(
                $"Se agotó el rango autorizado de facturas (hasta {hasta}). Registre el nuevo rango en Configuración > Facturación.");
    }

    public long? Restantes(long siguiente) => RangoHasta is { } hasta ? Math.Max(0, hasta - siguiente + 1) : null;
}
