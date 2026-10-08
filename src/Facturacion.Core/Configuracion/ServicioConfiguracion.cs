using System.Text.Json;
using System.Text.Json.Serialization;
using Facturacion.Core.Comun;
using Facturacion.Core.Facturas;

namespace Facturacion.Core.Configuracion;

/// <summary>Lee, valida y guarda la configuración (una sección JSON por clave).</summary>
public sealed class ServicioConfiguracion(IConfiguracionRepositorio repositorio, IFacturaRepositorio facturas)
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Ajustes Obtener() => new()
    {
        Negocio = Leer<DatosNegocio>(ClavesConfiguracion.Negocio),
        Facturacion = ObtenerFacturacion(),
        Impresion = Leer<AjustesImpresion>(ClavesConfiguracion.Impresion)
    };

    public DatosNegocio ObtenerNegocio() => Leer<DatosNegocio>(ClavesConfiguracion.Negocio);

    public AjustesImpresion ObtenerImpresion() => Leer<AjustesImpresion>(ClavesConfiguracion.Impresion);

    public AjustesFacturacion ObtenerFacturacion()
    {
        var ajustes = Leer<AjustesFacturacion>(ClavesConfiguracion.Facturacion);
        ajustes.SiguienteNumero = facturas.LeerSiguienteNumero();
        return ajustes;
    }

    public ConfiguracionIva ObtenerIva()
    {
        var f = ObtenerFacturacion();
        return new ConfiguracionIva(f.IvaHabilitado, f.IvaPorcentaje, f.PreciosIncluyenIva);
    }

    public DatosNegocio GuardarNegocio(DatosNegocio datos)
    {
        var limpio = new DatosNegocio
        {
            NombreComercial = Requerido(Texto.Limpiar(datos.NombreComercial), "el nombre comercial", 80),
            Giro = Largo(Texto.Limpiar(datos.Giro), "el giro", 80),
            Propietario = Largo(Texto.Limpiar(datos.Propietario), "el propietario", 80),
            Direccion = Largo(Texto.Limpiar(datos.Direccion), "la dirección", 160),
            Telefono = Largo(Identificacion.Telefonos(datos.Telefono), "los teléfonos", 60),
            Email = Largo(Texto.Limpiar(datos.Email), "el correo", 80),
            Ruc = Identificacion.Ruc(datos.Ruc) ?? "",
            Condiciones = Largo((datos.Condiciones ?? "").Trim(), "las condiciones", 2000)
        };
        Escribir(ClavesConfiguracion.Negocio, limpio);
        return limpio;
    }

    public AjustesFacturacion GuardarFacturacion(AjustesFacturacion datos)
    {
        var serie = Texto.Mayusculas(datos.Serie);
        if (serie.Length is 0 or > 5)
            throw new ReglaNegocioException("La serie debe tener entre 1 y 5 caracteres (ej. A).");
        if (datos.SiguienteNumero < 1)
            throw new ReglaNegocioException("El siguiente número de factura debe ser mayor que cero.");
        if (datos.RangoDesde.HasValue != datos.RangoHasta.HasValue)
            throw new ReglaNegocioException("Indique el rango completo (desde y hasta) o deje ambos vacíos.");
        if (datos.RangoDesde is { } desde && datos.RangoHasta is { } hasta)
        {
            if (desde < 1 || desde > hasta)
                throw new ReglaNegocioException("El rango autorizado no es válido: 'desde' debe ser menor o igual que 'hasta'.");
            if (datos.SiguienteNumero < desde || datos.SiguienteNumero > hasta)
                throw new ReglaNegocioException($"El siguiente número ({datos.SiguienteNumero}) está fuera del rango autorizado {desde}–{hasta}.");
        }
        if (datos.IvaPorcentaje is < 0 or > 100)
            throw new ReglaNegocioException("El porcentaje de IVA debe estar entre 0 y 100.");
        if (facturas.ExisteNumero(serie, datos.SiguienteNumero))
            throw new ReglaNegocioException($"La factura {serie}-{datos.SiguienteNumero} ya fue emitida. Indique un número que no se haya usado.");

        var limpio = new AjustesFacturacion
        {
            Serie = serie,
            TipoFactura = Largo(Texto.Mayusculas(datos.TipoFactura), "el tipo de factura", 20),
            SiguienteNumero = datos.SiguienteNumero,
            RangoDesde = datos.RangoDesde,
            RangoHasta = datos.RangoHasta,
            IvaHabilitado = datos.IvaHabilitado,
            IvaPorcentaje = Dinero.Redondear(datos.IvaPorcentaje),
            PreciosIncluyenIva = datos.PreciosIncluyenIva,
            PermitirStockNegativo = datos.PermitirStockNegativo
        };
        Escribir(ClavesConfiguracion.Facturacion, limpio);
        repositorio.Escribir(ClavesConfiguracion.SiguienteNumero, limpio.SiguienteNumero.ToString());
        return limpio;
    }

    public AjustesImpresion GuardarImpresion(AjustesImpresion datos)
    {
        if (datos.Copias is < 1 or > 5)
            throw new ReglaNegocioException("Las copias deben estar entre 1 y 5.");
        if (Math.Abs(datos.DesplazamientoXMm) > 30 || Math.Abs(datos.DesplazamientoYMm) > 30)
            throw new ReglaNegocioException("El desplazamiento de calibración debe estar entre -30 y 30 mm.");
        ValidarPlantilla(datos.Plantilla ?? throw new ReglaNegocioException("Falta la plantilla del formato preimpreso."));

        datos.Impresora = Texto.LimpiarONulo(datos.Impresora);
        Escribir(ClavesConfiguracion.Impresion, datos);
        return datos;
    }

    private static void ValidarPlantilla(PlantillaPreimpresa p)
    {
        if (p.TamanoFuentePt is < 6 or > 16)
            throw new ReglaNegocioException("El tamaño de letra del preimpreso debe estar entre 6 y 16 pt.");
        if (p.AnchoPaginaMm is < 50 or > 400 || p.AltoPaginaMm is < 50 or > 400)
            throw new ReglaNegocioException("Las medidas de la página del preimpreso no son válidas.");
        if (p.Filas is null || p.Filas.Maximo is < 1 or > 60 || p.Filas.Alto is < 3 or > 20)
            throw new ReglaNegocioException("Revise las filas del preimpreso: máximo entre 1 y 60, alto entre 3 y 20 mm.");

        var posiciones = new[] { p.Dia, p.Mes, p.Anio, p.Cliente, p.Direccion, p.Total };
        var columnas = new[] { p.Cantidad, p.Descripcion, p.PrecioUnitario, p.Importe };
        if (posiciones.Any(x => x is null) || columnas.Any(x => x is null))
            throw new ReglaNegocioException("La plantilla del preimpreso está incompleta. Use 'Restablecer plantilla'.");
        if (posiciones.Any(x => x.X is < 0 or > 400 || x.Y is < 0 or > 400 || x.Ancho is <= 0 or > 400)
            || columnas.Any(x => x.X is < 0 or > 400 || x.Ancho is <= 0 or > 400)
            || p.Filas.Y is < 0 or > 400)
            throw new ReglaNegocioException("Las posiciones del preimpreso deben estar entre 0 y 400 mm.");
        if (posiciones.Any(x => x.Alto is < 3 or > 30))
            throw new ReglaNegocioException("El alto de cada casilla del preimpreso debe estar entre 3 y 30 mm.");
    }

    private T Leer<T>(string clave) where T : new() => DesdeJson<T>(repositorio.Leer(clave));

    private void Escribir<T>(string clave, T valor) =>
        repositorio.Escribir(clave, JsonSerializer.Serialize(valor, OpcionesJson));

    /// <summary>Serializa con el mismo formato que usa la tabla de configuración (para datos iniciales).</summary>
    public static string ComoJson<T>(T valor) => JsonSerializer.Serialize(valor, OpcionesJson);

    /// <summary>Lee una sección guardada; si falta o está dañada devuelve los valores predeterminados.</summary>
    public static T DesdeJson<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(json, OpcionesJson) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    private static string Requerido(string valor, string campo, int largoMaximo)
    {
        if (valor.Length == 0) throw new ReglaNegocioException($"Indique {campo}.");
        return Largo(valor, campo, largoMaximo);
    }

    private static string Largo(string valor, string campo, int largoMaximo) =>
        valor.Length > largoMaximo
            ? throw new ReglaNegocioException($"El texto de {campo} es muy largo (máximo {largoMaximo} caracteres).")
            : valor;
}
