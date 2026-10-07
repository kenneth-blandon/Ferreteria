using Facturacion.Core.Comun;

namespace Facturacion.Core.Catalogo;

/// <summary>Reglas del catálogo de productos y categorías.</summary>
public sealed class ServicioCatalogo(IProductoRepositorio productos, ICategoriaRepositorio categorias, TimeProvider reloj)
{
    public const int MinimoCaracteresBusqueda = 2;
    public const int LimiteBusquedaPredeterminado = 20;
    public const int LimiteBusquedaMaximo = 50;

    // ---------- Consultas ----------

    /// <summary>Autocompletado del buscador: requiere al menos 2 caracteres y devuelve como máximo 50.</summary>
    public IReadOnlyList<Producto> Buscar(string? consulta, int limite = LimiteBusquedaPredeterminado)
    {
        var terminos = Texto.Terminos(consulta);
        if (terminos.Sum(t => t.Length) < MinimoCaracteresBusqueda) return [];
        return productos.Buscar(terminos, Texto.Mayusculas(consulta), Math.Clamp(limite, 1, LimiteBusquedaMaximo));
    }

    /// <summary>Búsqueda exacta por código interno o de barras (lector de código de barras).</summary>
    public Producto ObtenerPorCodigo(string? codigo)
    {
        var limpio = Texto.Mayusculas(codigo);
        if (limpio.Length == 0) throw new ReglaNegocioException("Indique un código.");
        return productos.ObtenerPorCodigo(limpio)
            ?? throw new NoEncontradoException($"No existe un producto con el código '{limpio}'.");
    }

    public Producto Obtener(long id) =>
        productos.Obtener(id) ?? throw new NoEncontradoException("El producto no existe.");

    public Pagina<Producto> Listar(FiltroProductos filtro)
    {
        var normalizado = filtro with
        {
            Pagina = Math.Max(1, filtro.Pagina),
            TamanoPagina = Math.Clamp(filtro.TamanoPagina, 10, 200)
        };
        return productos.Listar(normalizado, Texto.Terminos(filtro.Texto));
    }

    public IReadOnlyList<Producto> ListarTodos() => productos.ListarTodos();

    public IReadOnlyList<Producto> BajoStock() => productos.BajoStock();

    // ---------- Productos ----------

    public Producto Crear(DatosProducto datos)
    {
        var producto = Normalizar(datos, idExistente: null);
        if (datos.StockInicial < 0 || datos.StockInicial > Dinero.CantidadMaxima)
            throw new ReglaNegocioException("Las existencias iniciales no son válidas.");
        producto.Stock = Dinero.RedondearCantidad(datos.StockInicial);

        var id = productos.Crear(producto, Ahora());
        return Obtener(id);
    }

    public Producto Actualizar(long id, DatosProducto datos)
    {
        var actual = Obtener(id);
        var producto = Normalizar(datos, idExistente: id);
        producto.Id = actual.Id;
        producto.Stock = actual.Stock;
        productos.Actualizar(producto, Ahora());
        return Obtener(id);
    }

    /// <summary>Cambio rápido de precio desde la lista del catálogo.</summary>
    public Producto CambiarPrecio(long id, decimal precio)
    {
        Obtener(id);
        productos.ActualizarPrecio(id, ValidarPrecio(precio), Ahora());
        return Obtener(id);
    }

    public void Eliminar(long id)
    {
        Obtener(id);
        productos.Eliminar(id);
    }

    // ---------- Categorías ----------

    public IReadOnlyList<Categoria> Categorias() => categorias.Listar();

    public Categoria CrearCategoria(string? nombre)
    {
        var limpio = NombreCategoria(nombre, excluirId: null);
        var id = categorias.Crear(limpio);
        return categorias.Obtener(id)!;
    }

    public Categoria RenombrarCategoria(long id, string? nombre)
    {
        _ = categorias.Obtener(id) ?? throw new NoEncontradoException("La categoría no existe.");
        categorias.Renombrar(id, NombreCategoria(nombre, excluirId: id));
        return categorias.Obtener(id)!;
    }

    public void EliminarCategoria(long id)
    {
        _ = categorias.Obtener(id) ?? throw new NoEncontradoException("La categoría no existe.");
        categorias.Eliminar(id);
    }

    // ---------- Validación ----------

    private Producto Normalizar(DatosProducto datos, long? idExistente)
    {
        var codigo = Texto.Mayusculas(datos.Codigo);
        if (codigo.Length == 0)
        {
            if (idExistente is not null) throw new ReglaNegocioException("El código es obligatorio.");
            codigo = productos.SiguienteCodigoAutomatico();
        }
        if (codigo.Length > 40) throw new ReglaNegocioException("El código admite máximo 40 caracteres.");
        if (productos.ExisteCodigo(codigo, idExistente))
            throw new ReglaNegocioException($"Ya existe otro producto con el código '{codigo}'.");

        var barras = Texto.MayusculasONulo(datos.CodigoBarras);
        if (barras is { Length: > 64 }) throw new ReglaNegocioException("El código de barras admite máximo 64 caracteres.");
        if (barras is not null && productos.ExisteCodigoBarras(barras, idExistente))
            throw new ReglaNegocioException($"El código de barras '{barras}' ya pertenece a otro producto.");

        var nombre = Texto.Mayusculas(datos.Nombre);
        if (nombre.Length == 0) throw new ReglaNegocioException("El nombre del producto es obligatorio.");
        if (nombre.Length > 200) throw new ReglaNegocioException("El nombre admite máximo 200 caracteres.");

        var unidad = Texto.Mayusculas(datos.Unidad);
        if (unidad.Length == 0) unidad = "UND";
        if (unidad.Length > 12) throw new ReglaNegocioException("La unidad admite máximo 12 caracteres (ej. UND, MTS, LBS).");

        if (datos.CategoriaId is { } categoriaId && categorias.Obtener(categoriaId) is null)
            throw new ReglaNegocioException("La categoría seleccionada no existe.");

        if (datos.StockMinimo < 0 || datos.StockMinimo > Dinero.CantidadMaxima)
            throw new ReglaNegocioException("El stock mínimo no es válido.");

        return new Producto
        {
            Codigo = codigo,
            CodigoBarras = barras,
            Nombre = nombre,
            CategoriaId = datos.CategoriaId,
            Unidad = unidad,
            Precio = ValidarPrecio(datos.Precio),
            StockMinimo = Dinero.RedondearCantidad(datos.StockMinimo)
        };
    }

    private static decimal ValidarPrecio(decimal precio)
    {
        if (precio < 0 || precio > Dinero.MontoMaximo)
            throw new ReglaNegocioException("El precio no es válido.");
        return Dinero.Redondear(precio);
    }

    private string NombreCategoria(string? nombre, long? excluirId)
    {
        var limpio = Texto.Mayusculas(nombre);
        if (limpio.Length == 0) throw new ReglaNegocioException("Indique el nombre de la categoría.");
        if (limpio.Length > 60) throw new ReglaNegocioException("El nombre de la categoría admite máximo 60 caracteres.");
        if (categorias.ExisteNombre(limpio, excluirId))
            throw new ReglaNegocioException($"Ya existe la categoría '{limpio}'.");
        return limpio;
    }

    private DateTime Ahora() => reloj.AhoraLocal();
}
