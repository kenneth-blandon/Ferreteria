using Dapper;
using Facturacion.Core.Catalogo;
using Facturacion.Core.Comun;
using Facturacion.Core.Importacion;
using Facturacion.Core.Inventario;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos.Repositorios;

public sealed class ProductoRepositorio(ConexionSqlite db) : IProductoRepositorio
{
    private const string Consulta = """
        SELECT p.id, p.codigo, p.codigo_barras, p.nombre, p.categoria_id, c.nombre AS categoria,
               p.unidad, p.precio, p.stock, p.stock_minimo, p.actualizado
        FROM Productos p
        LEFT JOIN Categorias c ON c.id = p.categoria_id
        """;

    public Producto? Obtener(long id)
    {
        using var cn = db.Abrir();
        return cn.QuerySingleOrDefault<Producto>($"{Consulta} WHERE p.id = @id", new { id });
    }

    public Producto? ObtenerPorCodigo(string codigo)
    {
        using var cn = db.Abrir();
        return cn.QueryFirstOrDefault<Producto>(
            $"{Consulta} WHERE p.codigo = @codigo OR p.codigo_barras = @codigo ORDER BY p.codigo = @codigo DESC LIMIT 1",
            new { codigo });
    }

    public IReadOnlyList<Producto> ObtenerVarios(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0) return [];
        using var cn = db.Abrir();
        return cn.Query<Producto>($"{Consulta} WHERE p.id IN @ids", new { ids }).ToList();
    }

    public IReadOnlyList<Producto> Buscar(IReadOnlyList<string> terminos, string consultaExacta, int limite)
    {
        if (terminos.Count == 0) return [];
        var (filtro, parametros) = FiltroTerminos(terminos);
        parametros.Add("exacta", consultaExacta);
        parametros.Add("prefijoCodigo", Sql.EmpiezaCon(consultaExacta));
        parametros.Add("prefijoNombre", Sql.EmpiezaCon(terminos[0]));
        parametros.Add("limite", limite);

        using var cn = db.Abrir();
        return cn.Query<Producto>($"""
            {Consulta}
            WHERE {filtro}
            ORDER BY CASE
                         WHEN p.codigo = @exacta OR p.codigo_barras = @exacta THEN 0
                         WHEN p.codigo LIKE @prefijoCodigo ESCAPE '\' THEN 1
                         WHEN p.busqueda LIKE @prefijoNombre ESCAPE '\' THEN 2
                         ELSE 3
                     END,
                     p.nombre
            LIMIT @limite
            """, parametros).ToList();
    }

    public Pagina<Producto> Listar(FiltroProductos filtro, IReadOnlyList<string> terminos)
    {
        var (condicion, parametros) = FiltroTerminos(terminos);
        if (filtro.CategoriaId is { } categoriaId)
        {
            condicion += " AND p.categoria_id = @categoriaId";
            parametros.Add("categoriaId", categoriaId);
        }
        if (filtro.SoloBajoStock)
            condicion += " AND p.stock_minimo > 0 AND p.stock <= p.stock_minimo";

        parametros.Add("limite", filtro.TamanoPagina);
        parametros.Add("salto", (filtro.Pagina - 1) * filtro.TamanoPagina);

        using var cn = db.Abrir();
        var total = cn.ExecuteScalar<int>($"SELECT COUNT(*) FROM Productos p WHERE {condicion}", parametros);
        var elementos = cn.Query<Producto>(
            $"{Consulta} WHERE {condicion} ORDER BY p.nombre LIMIT @limite OFFSET @salto", parametros).ToList();
        return new Pagina<Producto>(elementos, total, filtro.Pagina, filtro.TamanoPagina);
    }

    public IReadOnlyList<Producto> ListarTodos()
    {
        using var cn = db.Abrir();
        return cn.Query<Producto>($"{Consulta} ORDER BY c.nombre IS NULL, c.nombre, p.nombre").ToList();
    }

    public IReadOnlyList<Producto> BajoStock()
    {
        using var cn = db.Abrir();
        return cn.Query<Producto>(
            $"{Consulta} WHERE p.stock_minimo > 0 AND p.stock <= p.stock_minimo ORDER BY p.stock - p.stock_minimo, p.nombre")
            .ToList();
    }

    public bool ExisteCodigo(string codigo, long? excluirId = null)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM Productos WHERE codigo = @codigo AND (@excluirId IS NULL OR id <> @excluirId))",
            new { codigo, excluirId });
    }

    public bool ExisteCodigoBarras(string codigoBarras, long? excluirId = null)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM Productos WHERE codigo_barras = @codigoBarras AND (@excluirId IS NULL OR id <> @excluirId))",
            new { codigoBarras, excluirId });
    }

    public string SiguienteCodigoAutomatico()
    {
        using var cn = db.Abrir();
        var mayor = cn.ExecuteScalar<long?>(
            "SELECT MAX(CAST(SUBSTR(codigo, 2) AS INTEGER)) FROM Productos WHERE codigo GLOB 'P[0-9][0-9][0-9][0-9][0-9]'") ?? 0;
        string codigo;
        do
        {
            codigo = $"P{++mayor:00000}";
        } while (cn.ExecuteScalar<bool>("SELECT EXISTS (SELECT 1 FROM Productos WHERE codigo = @codigo)", new { codigo }));
        return codigo;
    }

    public long Crear(Producto producto, DateTime fecha)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();
        var id = cn.ExecuteScalar<long>("""
            INSERT INTO Productos (codigo, codigo_barras, nombre, categoria_id, unidad, precio, stock, stock_minimo, busqueda, creado, actualizado)
            VALUES (@Codigo, @CodigoBarras, @Nombre, @CategoriaId, @Unidad, @Precio, @Stock, @StockMinimo, @Busqueda, @Fecha, @Fecha)
            RETURNING id
            """,
            new
            {
                producto.Codigo,
                producto.CodigoBarras,
                producto.Nombre,
                producto.CategoriaId,
                producto.Unidad,
                producto.Precio,
                producto.Stock,
                producto.StockMinimo,
                Busqueda = ClaveBusqueda(cn, tx, producto.Nombre, producto.Codigo, producto.CodigoBarras, producto.CategoriaId),
                Fecha = Sql.Fecha(fecha)
            }, tx);

        if (producto.Stock != 0)
            InventarioRepositorio.Registrar(cn, tx, id, fecha, TipoMovimiento.Inicial, producto.Stock, producto.Stock, null, "Existencia inicial");

        tx.Commit();
        return id;
    }

    public void Actualizar(Producto producto, DateTime fecha)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();
        cn.Execute("""
            UPDATE Productos
            SET codigo = @Codigo, codigo_barras = @CodigoBarras, nombre = @Nombre, categoria_id = @CategoriaId,
                unidad = @Unidad, precio = @Precio, stock_minimo = @StockMinimo, busqueda = @Busqueda, actualizado = @Fecha
            WHERE id = @Id
            """,
            new
            {
                producto.Id,
                producto.Codigo,
                producto.CodigoBarras,
                producto.Nombre,
                producto.CategoriaId,
                producto.Unidad,
                producto.Precio,
                producto.StockMinimo,
                Busqueda = ClaveBusqueda(cn, tx, producto.Nombre, producto.Codigo, producto.CodigoBarras, producto.CategoriaId),
                Fecha = Sql.Fecha(fecha)
            }, tx);
        tx.Commit();
    }

    public void ActualizarPrecio(long id, decimal precio, DateTime fecha)
    {
        using var cn = db.Abrir();
        cn.Execute("UPDATE Productos SET precio = @precio, actualizado = @fecha WHERE id = @id",
            new { id, precio, fecha = Sql.Fecha(fecha) });
    }

    public void Eliminar(long id)
    {
        using var cn = db.Abrir();
        cn.Execute("DELETE FROM Productos WHERE id = @id", new { id });
    }

    public ResultadoCarga CargarMasivo(IReadOnlyList<FilaProducto> filas, DateTime fecha)
    {
        using var cn = db.Abrir();
        // Una sola transacción: miles de filas se escriben en disco de una vez (milisegundos).
        using var tx = cn.BeginTransaction();

        var categorias = cn.Query<Categoria>("SELECT id, nombre FROM Categorias", transaction: tx)
            .ToDictionary(c => c.Nombre, c => c.Id, StringComparer.OrdinalIgnoreCase);
        var fechaTexto = Sql.Fecha(fecha);
        int insertados = 0, actualizados = 0;
        var errores = new List<ErrorImportacion>();

        foreach (var fila in filas)
        {
            try
            {
                long? categoriaId = null;
                if (fila.Categoria is { } nombreCategoria)
                {
                    if (!categorias.TryGetValue(nombreCategoria, out var idCategoria))
                    {
                        idCategoria = cn.ExecuteScalar<long>(
                            "INSERT INTO Categorias (nombre) VALUES (@nombreCategoria) RETURNING id", new { nombreCategoria }, tx);
                        categorias[nombreCategoria] = idCategoria;
                    }
                    categoriaId = idCategoria;
                }

                var existente = cn.QuerySingleOrDefault<ProductoExistente>("""
                    SELECT p.id, p.stock, p.codigo_barras, c.nombre AS categoria
                    FROM Productos p LEFT JOIN Categorias c ON c.id = p.categoria_id
                    WHERE p.codigo = @Codigo
                    """, new { fila.Codigo }, tx);

                // Las columnas vacías conservan el valor actual del producto.
                var barrasFinal = fila.CodigoBarras ?? existente?.CodigoBarras;
                var categoriaFinal = fila.Categoria ?? existente?.Categoria;

                var resultado = cn.QuerySingle<ProductoExistente>("""
                    INSERT INTO Productos (codigo, codigo_barras, nombre, categoria_id, unidad, precio, stock, stock_minimo, busqueda, creado, actualizado)
                    VALUES (@Codigo, @CodigoBarras, @Nombre, @CategoriaId, COALESCE(@Unidad, 'UND'), @Precio,
                            COALESCE(@Stock, 0), COALESCE(@StockMinimo, 0), @Busqueda, @Fecha, @Fecha)
                    ON CONFLICT(codigo) DO UPDATE SET
                        nombre        = excluded.nombre,
                        precio        = excluded.precio,
                        codigo_barras = COALESCE(@CodigoBarras, Productos.codigo_barras),
                        categoria_id  = COALESCE(@CategoriaId, Productos.categoria_id),
                        unidad        = COALESCE(@Unidad, Productos.unidad),
                        stock         = COALESCE(@Stock, Productos.stock),
                        stock_minimo  = COALESCE(@StockMinimo, Productos.stock_minimo),
                        busqueda      = excluded.busqueda,
                        actualizado   = excluded.actualizado
                    RETURNING id, stock
                    """,
                    new
                    {
                        fila.Codigo,
                        fila.CodigoBarras,
                        fila.Nombre,
                        CategoriaId = categoriaId,
                        fila.Unidad,
                        fila.Precio,
                        fila.Stock,
                        fila.StockMinimo,
                        Busqueda = Texto.ClaveBusqueda(fila.Nombre, fila.Codigo, barrasFinal, categoriaFinal),
                        Fecha = fechaTexto
                    }, tx);

                var anterior = existente?.Stock ?? 0;
                if (resultado.Stock != anterior)
                    InventarioRepositorio.Registrar(cn, tx, resultado.Id, fecha, TipoMovimiento.Importacion,
                        resultado.Stock - anterior, resultado.Stock, null, "Importación de catálogo");

                if (existente is null) insertados++; else actualizados++;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == Sql.ErrorRestriccion)
            {
                var mensaje = ex.Message.Contains("codigo_barras", StringComparison.OrdinalIgnoreCase)
                    ? $"El código de barras '{fila.CodigoBarras}' ya pertenece a otro producto."
                    : "El registro no cumple las reglas de la base de datos: " + ex.Message;
                errores.Add(new ErrorImportacion(fila.Fila, mensaje));
            }
        }

        tx.Commit();
        return new ResultadoCarga(insertados, actualizados, errores);
    }

    /// <summary>Todos los términos deben aparecer en el texto de búsqueda.</summary>
    private static (string Condicion, DynamicParameters Parametros) FiltroTerminos(IReadOnlyList<string> terminos)
    {
        var parametros = new DynamicParameters();
        if (terminos.Count == 0) return ("1 = 1", parametros);

        var condiciones = new List<string>(terminos.Count);
        for (var i = 0; i < terminos.Count; i++)
        {
            condiciones.Add($"p.busqueda LIKE @t{i} ESCAPE '\\'");
            parametros.Add($"t{i}", Sql.Contiene(terminos[i]));
        }
        return (string.Join(" AND ", condiciones), parametros);
    }

    internal static string ClaveBusqueda(SqliteConnection cn, SqliteTransaction tx, string nombre, string codigo, string? barras, long? categoriaId)
    {
        var categoria = categoriaId is null
            ? null
            : cn.ExecuteScalar<string?>("SELECT nombre FROM Categorias WHERE id = @categoriaId", new { categoriaId }, tx);
        return Texto.ClaveBusqueda(nombre, codigo, barras, categoria);
    }

    private sealed class ProductoExistente
    {
        public long Id { get; set; }
        public decimal Stock { get; set; }
        public string? CodigoBarras { get; set; }
        public string? Categoria { get; set; }
    }
}
