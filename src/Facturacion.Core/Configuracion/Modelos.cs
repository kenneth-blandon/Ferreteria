namespace Facturacion.Core.Configuracion;

/// <summary>Toda la configuración de la aplicación agrupada por sección.</summary>
public sealed class Ajustes
{
    public DatosNegocio Negocio { get; set; } = new();
    public AjustesFacturacion Facturacion { get; set; } = new();
    public AjustesImpresion Impresion { get; set; } = new();
}

/// <summary>Datos del negocio que aparecen en la factura.</summary>
public sealed class DatosNegocio
{
    public string NombreComercial { get; set; } = "";
    public string Giro { get; set; } = "";
    public string Propietario { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public string Ruc { get; set; } = "";
    /// <summary>Texto al pie del ticket, ej. "¡Gracias por su compra!".</summary>
    public string MensajePie { get; set; } = "";
    /// <summary>Cláusulas impresas en la factura de página completa (una por línea).</summary>
    public string Condiciones { get; set; } = "";
}

/// <summary>Numeración e impuestos.</summary>
public sealed class AjustesFacturacion
{
    /// <summary>Serie del talonario, ej. "A".</summary>
    public string Serie { get; set; } = "A";
    /// <summary>Leyenda del tipo de factura, ej. "CONTADO".</summary>
    public string TipoFactura { get; set; } = "CONTADO";
    /// <summary>Número que tomará la próxima factura. Debe coincidir con el próximo formulario físico.</summary>
    public long SiguienteNumero { get; set; } = 1;
    /// <summary>Rango autorizado por DGI (opcional), ej. 39001–44000.</summary>
    public long? RangoDesde { get; set; }
    public long? RangoHasta { get; set; }
    /// <summary>Negocios en Cuota Fija no cobran IVA: por eso viene desactivado.</summary>
    public bool IvaHabilitado { get; set; }
    public decimal IvaPorcentaje { get; set; } = 15m;
    /// <summary>true: el precio de catálogo ya trae IVA. false: el IVA se suma al subtotal.</summary>
    public bool PreciosIncluyenIva { get; set; }
    /// <summary>Permite facturar aunque el sistema registre menos existencias.</summary>
    public bool PermitirStockNegativo { get; set; } = true;
}

public enum FormatoImpresion
{
    /// <summary>Página completa (inyección/láser), similar al talonario.</summary>
    Carta,
    /// <summary>Rollo de impresora térmica de 80 o 58 mm.</summary>
    Ticket,
    /// <summary>Solo los datos variables, alineados sobre el formulario preimpreso (matriz de punto).</summary>
    Preimpreso
}

/// <summary>Opciones de impresión.</summary>
public sealed class AjustesImpresion
{
    public FormatoImpresion Formato { get; set; } = FormatoImpresion.Carta;
    /// <summary>Nombre de la impresora de Windows. Vacío = impresora predeterminada.</summary>
    public string? Impresora { get; set; }
    public bool ImprimirAlCobrar { get; set; } = true;
    /// <summary>Muestra el cuadro de impresión en lugar de imprimir directo.</summary>
    public bool MostrarDialogo { get; set; }
    public int Copias { get; set; } = 1;
    /// <summary>Ancho del rollo térmico: 80 o 58 mm.</summary>
    public int AnchoTicketMm { get; set; } = 80;
    /// <summary>Calibración fina del formato preimpreso (mm, puede ser negativo).</summary>
    public decimal DesplazamientoXMm { get; set; }
    public decimal DesplazamientoYMm { get; set; }
    public PlantillaPreimpresa Plantilla { get; set; } = new();
}

/// <summary>
/// Posiciones (en milímetros desde la esquina superior izquierda) de cada dato
/// sobre el formulario preimpreso. Valores iniciales estimados para una hoja carta;
/// se ajustan con la página de calibración.
/// </summary>
public sealed class PlantillaPreimpresa
{
    public decimal AnchoPaginaMm { get; set; } = 216;
    public decimal AltoPaginaMm { get; set; } = 279;
    public decimal TamanoFuentePt { get; set; } = 10;

    public Posicion Dia { get; set; } = new(15, 37, 10);
    public Posicion Mes { get; set; } = new(27, 37, 10);
    public Posicion Anio { get; set; } = new(38, 37, 13);
    public Posicion Cliente { get; set; } = new(30, 47, 170);
    public Posicion Direccion { get; set; } = new(34, 53, 166);

    public FilasPlantilla Filas { get; set; } = new();
    public Columna Cantidad { get; set; } = new(11, 17);
    public Columna Descripcion { get; set; } = new(31, 122);
    public Columna PrecioUnitario { get; set; } = new(155, 25);
    public Columna Importe { get; set; } = new(182, 25);

    public Posicion Total { get; set; } = new(182, 214, 25);
}

public sealed class Posicion
{
    public Posicion() { }
    public Posicion(decimal x, decimal y, decimal ancho) => (X, Y, Ancho) = (x, y, ancho);
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Ancho { get; set; }
}

public sealed class Columna
{
    public Columna() { }
    public Columna(decimal x, decimal ancho) => (X, Ancho) = (x, ancho);
    public decimal X { get; set; }
    public decimal Ancho { get; set; }
}

public sealed class FilasPlantilla
{
    /// <summary>Posición vertical de la primera fila de productos.</summary>
    public decimal Y { get; set; } = 67;
    public decimal Alto { get; set; } = 6.6m;
    /// <summary>Líneas disponibles en el formulario.</summary>
    public int Maximo { get; set; } = 20;
}
