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
    /// <summary>Uno o varios teléfonos de 8 dígitos separados por "/".</summary>
    public string Telefono { get; set; } = "";
    public string Email { get; set; } = "";
    public string Ruc { get; set; } = "";
    /// <summary>Cláusulas del pagaré que se muestran en la vista previa (una por línea).</summary>
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

/// <summary>
/// Impresión sobre el talonario preimpreso (matriz de punto). Solo se imprimen los datos variables;
/// la vista previa en hoja carta sirve para ver cómo quedará la factura.
/// </summary>
public sealed class AjustesImpresion
{
    /// <summary>Nombre de la impresora de Windows. Vacío = impresora predeterminada.</summary>
    public string? Impresora { get; set; }
    /// <summary>Muestra el cuadro de impresión en lugar de imprimir directo.</summary>
    public bool MostrarDialogo { get; set; }
    public int Copias { get; set; } = 1;
    /// <summary>Calibración fina de todo el formato (mm, puede ser negativo).</summary>
    public decimal DesplazamientoXMm { get; set; }
    public decimal DesplazamientoYMm { get; set; }
    public PlantillaPreimpresa Plantilla { get; set; } = new();
}

/// <summary>
/// Posición de cada dato sobre el formulario preimpreso, en milímetros desde la esquina superior
/// izquierda de la hoja (216 × 279 mm). Cada campo es una caja: el texto se centra verticalmente.
/// Valores medidos sobre la foto del talonario "Electric Hammer" corrigiendo la perspectiva
/// (22 filas de 6.16 mm); se afinan con la hoja de calibración.
/// </summary>
public sealed class PlantillaPreimpresa
{
    public decimal AnchoPaginaMm { get; set; } = 216;
    public decimal AltoPaginaMm { get; set; } = 279;
    public decimal TamanoFuentePt { get; set; } = 10;

    /// <summary>Casillas DIA / MES / AÑO (fila de valores bajo los títulos).</summary>
    public Posicion Dia { get; set; } = new(8.8m, 42.1m, 11.3m, 7.6m);
    public Posicion Mes { get; set; } = new(20.1m, 42.1m, 12.5m, 7.6m);
    public Posicion Anio { get; set; } = new(32.6m, 42.1m, 11.8m, 7.6m);
    /// <summary>Franjas justo encima de las líneas de CLIENTE y DIRECCIÓN.</summary>
    public Posicion Cliente { get; set; } = new(25.5m, 53.2m, 179.6m, 5m);
    public Posicion Direccion { get; set; } = new(30.3m, 60m, 174.8m, 5m);

    public FilasPlantilla Filas { get; set; } = new();
    public Columna Cantidad { get; set; } = new(9m, 17.6m);
    public Columna Descripcion { get; set; } = new(28.5m, 121.4m);
    public Columna PrecioUnitario { get; set; } = new(151.7m, 25.3m);
    public Columna Importe { get; set; } = new(178.6m, 26.7m);

    /// <summary>Casilla del TOTAL (última fila de la tabla, columna TOTAL).</summary>
    public Posicion Total { get; set; } = new(178.6m, 209.9m, 26.7m, 6m);
}

public sealed class Posicion
{
    public Posicion() { }
    public Posicion(decimal x, decimal y, decimal ancho, decimal alto) => (X, Y, Ancho, Alto) = (x, y, ancho, alto);
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Ancho { get; set; }
    public decimal Alto { get; set; }
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
    /// <summary>Borde superior de la primera fila de productos.</summary>
    public decimal Y { get; set; } = 74.3m;
    public decimal Alto { get; set; } = 6.16m;
    /// <summary>Filas de productos del formulario (sin contar la del TOTAL).</summary>
    public int Maximo { get; set; } = 22;
}
