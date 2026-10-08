using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Core.Facturas;
using Facturacion.Core.Inventario;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos.Repositorios;

public sealed partial class FacturaRepositorio(ConexionSqlite db) : IFacturaRepositorio
{
    private const string ColumnasFactura = """
        id, serie, numero, fecha, cliente_id, cliente_nombre, cliente_ruc, cliente_direccion,
        subtotal, iva, total, metodo_pago, monto_recibido, vuelto, referencia,
        estado, motivo_anulacion, fecha_anulacion, fecha_modificacion
        """;

    private const string InsertarLinea = """
        INSERT INTO DetalleFactura (factura_id, producto_id, codigo, descripcion, unidad, cantidad, precio_unitario, total)
        VALUES (@FacturaId, @ProductoId, @Codigo, @Descripcion, @Unidad, @Cantidad, @PrecioUnitario, @Total)
        RETURNING id
        """;

    /// <summary>"40659" o "A-40659": búsqueda por número de factura.</summary>
    [GeneratedRegex(@"^(?:[A-Za-z]{1,5}\s*-?\s*)?(\d{1,12})$")]
    private static partial Regex PatronNumero();

    public Factura Emitir(Factura factura, ReglasNumeracion numeracion, bool permitirStockNegativo)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();

        var numero = LeerSiguienteNumero(cn, tx);
        numeracion.Validar(numero);
        if (ExisteNumero(cn, tx, numeracion.Serie, numero))
            throw new ReglaNegocioException(
                $"El número {numeracion.Serie}-{numero} ya fue utilizado. Corrija el 'Siguiente número' en Configuración > Facturación.");

        if (!permitirStockNegativo) VerificarExistencias(cn, tx, factura.Lineas);

        factura.Serie = numeracion.Serie;
        factura.Numero = numero;
        factura.Estado = EstadoFactura.Emitida;
        factura.Id = cn.ExecuteScalar<long>("""
            INSERT INTO Facturas (serie, numero, fecha, cliente_id, cliente_nombre, cliente_ruc, cliente_direccion,
                                  subtotal, iva, total, metodo_pago, monto_recibido, vuelto, referencia, estado)
            VALUES (@Serie, @Numero, @Fecha, @ClienteId, @ClienteNombre, @ClienteRuc, @ClienteDireccion,
                    @Subtotal, @Iva, @Total, @MetodoPago, @MontoRecibido, @Vuelto, @Referencia, @Estado)
            RETURNING id
            """,
            new
            {
                factura.Serie,
                factura.Numero,
                Fecha = Sql.Fecha(factura.Fecha),
                factura.ClienteId,
                factura.ClienteNombre,
                factura.ClienteRuc,
                factura.ClienteDireccion,
                factura.Subtotal,
                factura.Iva,
                factura.Total,
                MetodoPago = factura.MetodoPago.ToString(),
                factura.MontoRecibido,
                factura.Vuelto,
                factura.Referencia,
                Estado = factura.Estado.ToString()
            }, tx);

        var referencia = $"Factura {factura.NumeroCompleto}";
        foreach (var linea in factura.Lineas)
        {
            linea.FacturaId = factura.Id;
            linea.Id = cn.ExecuteScalar<long>(InsertarLinea, linea, tx);

            if (linea.ProductoId is { } productoId)
                MoverExistencia(cn, tx, productoId, -linea.Cantidad, factura.Fecha, TipoMovimiento.Venta, referencia);
        }

        GuardarSiguienteNumero(cn, tx, numero + 1);
        tx.Commit();
        return factura;
    }

    public Factura? Obtener(long id)
    {
        using var cn = db.Abrir();
        var factura = cn.QuerySingleOrDefault<Factura>($"SELECT {ColumnasFactura} FROM Facturas WHERE id = @id", new { id });
        if (factura is null) return null;

        factura.Lineas = cn.Query<LineaFactura>("""
            SELECT id, factura_id, producto_id, codigo, descripcion, unidad, cantidad, precio_unitario, total
            FROM DetalleFactura WHERE factura_id = @id ORDER BY id
            """, new { id }).ToList();

        factura.Modificaciones = cn.Query<ModificacionGuardada>("""
            SELECT id, factura_id, fecha, empleado, motivo, cambios, total_anterior, total_nuevo
            FROM ModificacionesFactura WHERE factura_id = @id ORDER BY fecha DESC, id DESC
            """, new { id })
            .Select(m => new ModificacionFactura
            {
                Id = m.Id,
                FacturaId = m.FacturaId,
                Fecha = m.Fecha,
                Empleado = m.Empleado,
                Motivo = m.Motivo,
                Cambios = JsonSerializer.Deserialize<List<string>>(m.Cambios) ?? [],
                TotalAnterior = m.TotalAnterior,
                TotalNuevo = m.TotalNuevo
            })
            .ToList();
        return factura;
    }

    public Factura Modificar(Factura anterior, Factura nueva, ModificacionFactura modificacion, bool permitirStockNegativo)
    {
        using (var cn = db.Abrir())
        using (var tx = cn.BeginTransaction())
        {
            var estado = cn.ExecuteScalar<string?>("SELECT estado FROM Facturas WHERE id = @Id", new { anterior.Id }, tx)
                ?? throw new NoEncontradoException("La factura no existe.");
            if (estado != nameof(EstadoFactura.Emitida))
                throw new ReglaNegocioException($"La factura {anterior.NumeroCompleto} está anulada y no se puede modificar.");

            // Diferencia neta por producto: lo facturado antes regresa al inventario y lo nuevo sale.
            var cambiosExistencia = new Dictionary<long, decimal>();
            foreach (var l in anterior.Lineas.Where(l => l.ProductoId.HasValue))
                cambiosExistencia[l.ProductoId!.Value] = cambiosExistencia.GetValueOrDefault(l.ProductoId!.Value) + l.Cantidad;
            foreach (var l in nueva.Lineas.Where(l => l.ProductoId.HasValue))
                cambiosExistencia[l.ProductoId!.Value] = cambiosExistencia.GetValueOrDefault(l.ProductoId!.Value) - l.Cantidad;

            if (!permitirStockNegativo)
            {
                foreach (var (productoId, cambio) in cambiosExistencia.Where(c => c.Value < 0))
                {
                    var producto = cn.QuerySingleOrDefault<Existencia>(
                        "SELECT nombre, unidad, stock FROM Productos WHERE id = @productoId", new { productoId }, tx);
                    if (producto is not null && producto.Stock + cambio < 0)
                        throw new ReglaNegocioException(
                            $"Existencia insuficiente de {producto.Nombre}: hay {Dinero.Cantidad(producto.Stock)} {producto.Unidad} " +
                            $"y la corrección necesita {Dinero.Cantidad(-cambio)} más.");
                }
            }

            cn.Execute("""
                UPDATE Facturas
                SET cliente_id = @ClienteId, cliente_nombre = @ClienteNombre, cliente_ruc = @ClienteRuc,
                    cliente_direccion = @ClienteDireccion, subtotal = @Subtotal, iva = @Iva, total = @Total,
                    metodo_pago = @MetodoPago, monto_recibido = @MontoRecibido, vuelto = @Vuelto,
                    referencia = @Referencia, fecha_modificacion = @FechaModificacion
                WHERE id = @Id
                """,
                new
                {
                    anterior.Id,
                    nueva.ClienteId,
                    nueva.ClienteNombre,
                    nueva.ClienteRuc,
                    nueva.ClienteDireccion,
                    nueva.Subtotal,
                    nueva.Iva,
                    nueva.Total,
                    MetodoPago = nueva.MetodoPago.ToString(),
                    nueva.MontoRecibido,
                    nueva.Vuelto,
                    nueva.Referencia,
                    FechaModificacion = Sql.Fecha(modificacion.Fecha)
                }, tx);

            cn.Execute("DELETE FROM DetalleFactura WHERE factura_id = @Id", new { anterior.Id }, tx);
            foreach (var linea in nueva.Lineas)
            {
                linea.FacturaId = anterior.Id;
                linea.Id = cn.ExecuteScalar<long>(InsertarLinea, linea, tx);
            }

            var referencia = $"Modificación {anterior.NumeroCompleto}";
            foreach (var (productoId, cambio) in cambiosExistencia.Where(c => c.Value != 0))
                MoverExistencia(cn, tx, productoId, cambio, modificacion.Fecha, TipoMovimiento.Modificacion, referencia);

            modificacion.Id = cn.ExecuteScalar<long>("""
                INSERT INTO ModificacionesFactura (factura_id, fecha, empleado, motivo, cambios, total_anterior, total_nuevo, datos_anteriores)
                VALUES (@FacturaId, @Fecha, @Empleado, @Motivo, @Cambios, @TotalAnterior, @TotalNuevo, @DatosAnteriores)
                RETURNING id
                """,
                new
                {
                    FacturaId = anterior.Id,
                    Fecha = Sql.Fecha(modificacion.Fecha),
                    modificacion.Empleado,
                    modificacion.Motivo,
                    Cambios = JsonSerializer.Serialize(modificacion.Cambios),
                    modificacion.TotalAnterior,
                    modificacion.TotalNuevo,
                    DatosAnteriores = CopiaParaConstancia(anterior)
                }, tx);

            tx.Commit();
        }
        return Obtener(anterior.Id)!;
    }

    /// <summary>Copia completa de la factura antes del cambio (queda como constancia).</summary>
    private static string CopiaParaConstancia(Factura f) => JsonSerializer.Serialize(new
    {
        f.Serie,
        f.Numero,
        f.Fecha,
        f.ClienteId,
        f.ClienteNombre,
        f.ClienteRuc,
        f.ClienteDireccion,
        f.Subtotal,
        f.Iva,
        f.Total,
        MetodoPago = f.MetodoPago.ToString(),
        f.MontoRecibido,
        f.Vuelto,
        f.Referencia,
        Lineas = f.Lineas.Select(l => new { l.ProductoId, l.Codigo, l.Descripcion, l.Unidad, l.Cantidad, l.PrecioUnitario, l.Total })
    });

    private sealed class ModificacionGuardada
    {
        public long Id { get; set; }
        public long FacturaId { get; set; }
        public DateTime Fecha { get; set; }
        public string Empleado { get; set; } = "";
        public string Motivo { get; set; } = "";
        public string Cambios { get; set; } = "[]";
        public decimal TotalAnterior { get; set; }
        public decimal TotalNuevo { get; set; }
    }

    public IReadOnlyList<Factura> Listar(FiltroFacturas filtro)
    {
        var condiciones = new List<string>();
        var parametros = new DynamicParameters();
        var texto = Texto.Limpiar(filtro.Texto);

        var coincidencia = PatronNumero().Match(texto);
        if (coincidencia.Success && long.TryParse(coincidencia.Groups[1].Value, out var numero))
        {
            // Un número de factura se busca en cualquier fecha.
            condiciones.Add("numero = @numero");
            parametros.Add("numero", numero);
        }
        else
        {
            condiciones.Add("fecha >= @desde AND fecha < @hasta");
            parametros.Add("desde", Sql.InicioDia(filtro.Desde));
            parametros.Add("hasta", Sql.InicioDia(filtro.Hasta.AddDays(1)));
            if (texto.Length > 0)
            {
                condiciones.Add("(cliente_nombre LIKE @texto ESCAPE '\\' OR cliente_ruc LIKE @texto ESCAPE '\\')");
                parametros.Add("texto", Sql.Contiene(Texto.Mayusculas(texto)));
            }
        }

        if (filtro.Estado is { } estado)
        {
            condiciones.Add("estado = @estado");
            parametros.Add("estado", estado.ToString());
        }

        using var cn = db.Abrir();
        return cn.Query<Factura>($"""
            SELECT {ColumnasFactura} FROM Facturas
            WHERE {string.Join(" AND ", condiciones)}
            ORDER BY fecha DESC, id DESC
            LIMIT 5000
            """, parametros).ToList();
    }

    public Factura Anular(long id, string motivo, DateTime fecha)
    {
        using (var cn = db.Abrir())
        using (var tx = cn.BeginTransaction())
        {
            var factura = cn.QuerySingleOrDefault<Factura>($"SELECT {ColumnasFactura} FROM Facturas WHERE id = @id", new { id }, tx)
                ?? throw new NoEncontradoException("La factura no existe.");
            if (factura.Estado == EstadoFactura.Anulada)
                throw new ReglaNegocioException($"La factura {factura.NumeroCompleto} ya está anulada.");

            cn.Execute("""
                UPDATE Facturas SET estado = @estado, motivo_anulacion = @motivo, fecha_anulacion = @fecha
                WHERE id = @id
                """, new { id, estado = EstadoFactura.Anulada.ToString(), motivo, fecha = Sql.Fecha(fecha) }, tx);

            var lineas = cn.Query<LineaFactura>(
                "SELECT producto_id, cantidad FROM DetalleFactura WHERE factura_id = @id AND producto_id IS NOT NULL",
                new { id }, tx);
            var referencia = $"Anulación {factura.NumeroCompleto}";
            foreach (var linea in lineas)
                MoverExistencia(cn, tx, linea.ProductoId!.Value, linea.Cantidad, fecha, TipoMovimiento.Anulacion, referencia);

            tx.Commit();
        }
        return Obtener(id)!;
    }

    public long LeerSiguienteNumero()
    {
        using var cn = db.Abrir();
        return LeerSiguienteNumero(cn, null);
    }

    public bool ExisteNumero(string serie, long numero)
    {
        using var cn = db.Abrir();
        return ExisteNumero(cn, null, serie, numero);
    }

    private static long LeerSiguienteNumero(SqliteConnection cn, SqliteTransaction? tx)
    {
        var valor = cn.ExecuteScalar<string?>("SELECT valor FROM Configuracion WHERE clave = @clave",
            new { clave = ClavesConfiguracion.SiguienteNumero }, tx);
        return long.TryParse(valor, out var numero) && numero > 0 ? numero : 1;
    }

    private static void GuardarSiguienteNumero(SqliteConnection cn, SqliteTransaction tx, long numero) =>
        cn.Execute("""
            INSERT INTO Configuracion (clave, valor) VALUES (@clave, @valor)
            ON CONFLICT(clave) DO UPDATE SET valor = excluded.valor
            """, new { clave = ClavesConfiguracion.SiguienteNumero, valor = numero.ToString() }, tx);

    private static bool ExisteNumero(SqliteConnection cn, SqliteTransaction? tx, string serie, long numero) =>
        cn.ExecuteScalar<bool>("SELECT EXISTS (SELECT 1 FROM Facturas WHERE serie = @serie AND numero = @numero)",
            new { serie, numero }, tx);

    private static void MoverExistencia(SqliteConnection cn, SqliteTransaction tx, long productoId, decimal cambio,
        DateTime fecha, TipoMovimiento tipo, string referencia)
    {
        var stock = cn.ExecuteScalar<decimal?>("""
            UPDATE Productos SET stock = ROUND(stock + @cambio, 3), actualizado = @fecha
            WHERE id = @productoId
            RETURNING stock
            """, new { productoId, cambio, fecha = Sql.Fecha(fecha) }, tx);

        // Si el producto se eliminó del catálogo no hay existencia que mover.
        if (stock is not null)
            InventarioRepositorio.Registrar(cn, tx, productoId, fecha, tipo, cambio, stock.Value, referencia, null);
    }

    private static void VerificarExistencias(SqliteConnection cn, SqliteTransaction tx, IEnumerable<LineaFactura> lineas)
    {
        var porProducto = lineas
            .Where(l => l.ProductoId.HasValue)
            .GroupBy(l => l.ProductoId!.Value)
            .Select(g => (Id: g.Key, Cantidad: g.Sum(l => l.Cantidad)));

        foreach (var (id, cantidad) in porProducto)
        {
            var producto = cn.QuerySingleOrDefault<Existencia>(
                "SELECT nombre, unidad, stock FROM Productos WHERE id = @id", new { id }, tx);
            if (producto is not null && producto.Stock < cantidad)
                throw new ReglaNegocioException(
                    $"Existencia insuficiente de {producto.Nombre}: hay {Dinero.Cantidad(producto.Stock)} {producto.Unidad} " +
                    $"y se quieren vender {Dinero.Cantidad(cantidad)}.");
        }
    }

    private sealed class Existencia
    {
        public string Nombre { get; set; } = "";
        public string Unidad { get; set; } = "";
        public decimal Stock { get; set; }
    }
}
