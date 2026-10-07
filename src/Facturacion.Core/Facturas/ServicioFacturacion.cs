using Facturacion.Core.Catalogo;
using Facturacion.Core.Clientes;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;

namespace Facturacion.Core.Facturas;

/// <summary>Emisión, consulta y anulación de facturas.</summary>
public sealed class ServicioFacturacion(
    IFacturaRepositorio facturas,
    IProductoRepositorio productos,
    IClienteRepositorio clientes,
    ServicioConfiguracion configuracion,
    TimeProvider reloj)
{
    public const int MaximoLineas = 200;

    /// <summary>
    /// Valida la venta, recalcula todos los montos (no se confía en los que calcula la pantalla)
    /// y la guarda con el siguiente número del talonario.
    /// </summary>
    public Factura Emitir(SolicitudFactura solicitud)
    {
        var lineasSolicitud = solicitud.Lineas ?? [];
        if (lineasSolicitud.Count == 0)
            throw new ReglaNegocioException("Agregue al menos un producto a la factura.");
        if (lineasSolicitud.Count > MaximoLineas)
            throw new ReglaNegocioException($"La factura admite máximo {MaximoLineas} líneas.");
        if (!Enum.IsDefined(solicitud.MetodoPago))
            throw new ReglaNegocioException("Seleccione un método de pago válido.");

        var ajustes = configuracion.ObtenerFacturacion();
        var lineas = ConstruirLineas(lineasSolicitud);
        var totales = CalculadoraFactura.Calcular(
            lineas.Select(l => l.Total),
            new ConfiguracionIva(ajustes.IvaHabilitado, ajustes.IvaPorcentaje, ajustes.PreciosIncluyenIva));

        var factura = new Factura
        {
            Fecha = reloj.AhoraLocal(),
            Subtotal = totales.Subtotal,
            Iva = totales.Iva,
            Total = totales.Total,
            MetodoPago = solicitud.MetodoPago,
            Lineas = lineas
        };

        AplicarPago(factura, solicitud);
        AplicarCliente(factura, solicitud.Cliente);

        var numeracion = new ReglasNumeracion(ajustes.Serie, ajustes.RangoDesde, ajustes.RangoHasta);
        return facturas.Emitir(factura, numeracion, ajustes.PermitirStockNegativo);
    }

    public Factura Obtener(long id) =>
        facturas.Obtener(id) ?? throw new NoEncontradoException("La factura no existe.");

    public IReadOnlyList<Factura> Listar(FiltroFacturas filtro)
    {
        if (filtro.Hasta < filtro.Desde)
            throw new ReglaNegocioException("La fecha 'hasta' debe ser posterior a la fecha 'desde'.");
        if (filtro.Hasta.DayNumber - filtro.Desde.DayNumber > 366)
            throw new ReglaNegocioException("Consulte como máximo un año a la vez.");
        return facturas.Listar(filtro);
    }

    /// <summary>Anula una factura emitida por error. Requiere el motivo y repone las existencias.</summary>
    public Factura Anular(long id, string? motivo)
    {
        var limpio = Texto.Limpiar(motivo);
        if (limpio.Length < 3) throw new ReglaNegocioException("Indique el motivo de la anulación.");
        if (limpio.Length > 200) throw new ReglaNegocioException("El motivo admite máximo 200 caracteres.");

        var factura = Obtener(id);
        if (factura.Estado == EstadoFactura.Anulada)
            throw new ReglaNegocioException($"La factura {factura.NumeroCompleto} ya está anulada.");

        return facturas.Anular(id, limpio, reloj.AhoraLocal());
    }

    public ProximoNumero ProximoNumero()
    {
        var ajustes = configuracion.ObtenerFacturacion();
        var reglas = new ReglasNumeracion(ajustes.Serie, ajustes.RangoDesde, ajustes.RangoHasta);
        var restantes = reglas.Restantes(ajustes.SiguienteNumero);

        string? advertencia = null;
        if (restantes == 0)
            advertencia = "Se agotó el rango autorizado. Registre el nuevo talonario en Configuración.";
        else if (restantes <= ReglasNumeracion.UmbralAviso)
            advertencia = $"Quedan {restantes} facturas del rango autorizado.";

        return new ProximoNumero(ajustes.Serie, ajustes.SiguienteNumero, ajustes.RangoHasta, restantes, advertencia);
    }

    private List<LineaFactura> ConstruirLineas(IReadOnlyList<LineaSolicitud> solicitud)
    {
        var ids = solicitud.Where(l => l.ProductoId.HasValue).Select(l => l.ProductoId!.Value).Distinct().ToList();
        var catalogo = productos.ObtenerVarios(ids).ToDictionary(p => p.Id);

        var lineas = new List<LineaFactura>(solicitud.Count);
        for (var i = 0; i < solicitud.Count; i++)
        {
            var entrada = solicitud[i];
            var numeroLinea = i + 1;

            var cantidad = Dinero.RedondearCantidad(entrada.Cantidad);
            if (cantidad <= 0 || cantidad > Dinero.CantidadMaxima)
                throw new ReglaNegocioException($"Línea {numeroLinea}: la cantidad debe ser mayor que cero.");
            if (entrada.Precio < 0 || entrada.Precio > Dinero.MontoMaximo)
                throw new ReglaNegocioException($"Línea {numeroLinea}: el precio no es válido.");
            var precio = Dinero.Redondear(entrada.Precio);

            LineaFactura linea;
            if (entrada.ProductoId is { } productoId)
            {
                if (!catalogo.TryGetValue(productoId, out var producto))
                    throw new ReglaNegocioException($"Línea {numeroLinea}: el producto ya no existe en el catálogo.");
                linea = new LineaFactura
                {
                    ProductoId = producto.Id,
                    Codigo = producto.Codigo,
                    Descripcion = producto.Nombre,
                    Unidad = producto.Unidad
                };
            }
            else
            {
                var descripcion = Texto.Mayusculas(entrada.Descripcion);
                if (descripcion.Length == 0)
                    throw new ReglaNegocioException($"Línea {numeroLinea}: escriba la descripción del artículo.");
                var unidad = Texto.Mayusculas(entrada.Unidad);
                linea = new LineaFactura
                {
                    Codigo = "",
                    Descripcion = Texto.Recortar(descripcion, 200),
                    Unidad = unidad.Length == 0 ? "UND" : Texto.Recortar(unidad, 12)
                };
            }

            linea.Cantidad = cantidad;
            linea.PrecioUnitario = precio;
            linea.Total = CalculadoraFactura.TotalLinea(cantidad, precio);
            lineas.Add(linea);
        }
        return lineas;
    }

    private static void AplicarPago(Factura factura, SolicitudFactura solicitud)
    {
        if (factura.MetodoPago == MetodoPago.Efectivo)
        {
            // Sin monto recibido se asume pago exacto.
            var recibido = solicitud.MontoRecibido is > 0 ? Dinero.Redondear(solicitud.MontoRecibido.Value) : factura.Total;
            if (recibido > Dinero.MontoMaximo) throw new ReglaNegocioException("El monto recibido no es válido.");
            factura.MontoRecibido = recibido;
            factura.Vuelto = CalculadoraFactura.Vuelto(factura.Total, recibido);
        }
        else
        {
            var referencia = Texto.MayusculasONulo(solicitud.Referencia);
            if (referencia is { Length: > 60 }) throw new ReglaNegocioException("La referencia admite máximo 60 caracteres.");
            factura.Referencia = referencia;
        }
    }

    private void AplicarCliente(Factura factura, ClienteSolicitud? solicitud)
    {
        var nombre = Texto.Mayusculas(solicitud?.Nombre);
        if (solicitud is null || nombre.Length == 0 || nombre == Factura.ConsumidorFinal)
        {
            factura.ClienteNombre = Factura.ConsumidorFinal;
            factura.ClienteRuc = Texto.MayusculasONulo(solicitud?.Ruc);
            factura.ClienteDireccion = Texto.MayusculasONulo(solicitud?.Direccion);
            return;
        }

        factura.ClienteNombre = Texto.Recortar(nombre, 120);
        factura.ClienteRuc = Texto.MayusculasONulo(solicitud.Ruc) is { } ruc ? Texto.Recortar(ruc, 30) : null;
        factura.ClienteDireccion = Texto.MayusculasONulo(solicitud.Direccion) is { } dir ? Texto.Recortar(dir, 200) : null;

        if (solicitud.Id is { } clienteId)
        {
            factura.ClienteId = clientes.Obtener(clienteId)?.Id;
        }
        else if (solicitud.Guardar && !clientes.ExisteNombre(factura.ClienteNombre))
        {
            factura.ClienteId = clientes.Crear(new Cliente
            {
                Nombre = factura.ClienteNombre,
                Ruc = factura.ClienteRuc,
                Direccion = factura.ClienteDireccion,
                Creado = factura.Fecha
            });
        }
    }
}
