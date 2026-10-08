using Facturacion.Core.Comun;

namespace Facturacion.Core.Catalogo;

/// <summary>Reglas del catálogo: productos, categorías, unidades y cambios en bloque.</summary>
public sealed class ServicioCatalogo(
    IProductoRepositorio productos,
    ICategoriaRepositorio categorias,
    IUnidadRepositorio unidades,
    TimeProvider reloj)
{
    public const int MinimoCaracteresBusqueda = 2;
    public const int LimiteBusquedaPredeterminado = 20;
    public const int LimiteBusquedaMaximo = 50;
    public const int MaximoMasivo = 10_000;

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

    // ---------- Unidades de medida ----------

    public IReadOnlyList<Unidad> Unidades() => unidades.Listar();

    public Unidad CrearUnidad(string? nombre)
    {
        var id = unidades.Crear(NombreUnidad(nombre, excluirId: null));
        return unidades.Obtener(id)!;
    }

    /// <summary>Renombrar una unidad la cambia también en todos los productos que la usan.</summary>
    public Unidad RenombrarUnidad(long id, string? nombre)
    {
        var actual = unidades.Obtener(id) ?? throw new NoEncontradoException("La unidad no existe.");
        if (actual.Nombre == Unidad.Predeterminada)
            throw new ReglaNegocioException($"La unidad {Unidad.Predeterminada} es la predeterminada y no se puede renombrar.");
        unidades.Renombrar(id, NombreUnidad(nombre, excluirId: id));
        return unidades.Obtener(id)!;
    }

    public void EliminarUnidad(long id)
    {
        var unidad = unidades.Obtener(id) ?? throw new NoEncontradoException("La unidad no existe.");
        if (unidad.Nombre == Unidad.Predeterminada)
            throw new ReglaNegocioException($"La unidad {Unidad.Predeterminada} es la predeterminada y no se puede eliminar.");
        if (unidad.TotalProductos > 0)
            throw new ReglaNegocioException(
                $"La unidad {unidad.Nombre} la usan {unidad.TotalProductos} productos. Cámbielos de unidad (puede hacerlo en bloque) antes de eliminarla.");
        unidades.Eliminar(id);
    }

    // ---------- Cambios en bloque ----------

    /// <summary>
    /// Cambia la categoría o la unidad de varios productos a la vez. Solo esos dos campos:
    /// nombre, precio, códigos y existencias no se pueden cambiar en bloque.
    /// </summary>
    public ResultadoMasivo ActualizarMasivo(SolicitudMasiva solicitud)
    {
        var ids = (solicitud.ProductoIds ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new ReglaNegocioException("Seleccione al menos un producto.");
        if (ids.Count > MaximoMasivo) throw new ReglaNegocioException($"Seleccione como máximo {MaximoMasivo:N0} productos a la vez.");
        if (productos.ContarExistentes(ids) != ids.Count)
            throw new ReglaNegocioException("Algunos productos seleccionados ya no existen. Actualice la lista e intente de nuevo.");

        switch (solicitud.Campo)
        {
            case CampoMasivo.Categoria:
            {
                string nombre = "SIN CATEGORÍA";
                if (solicitud.CategoriaId is { } categoriaId)
                    nombre = (categorias.Obtener(categoriaId) ?? throw new ReglaNegocioException("La categoría seleccionada no existe.")).Nombre;
                var cambiados = productos.CambiarCategoria(ids, solicitud.CategoriaId, Ahora());
                return new ResultadoMasivo(cambiados, CampoMasivo.Categoria, nombre);
            }
            case CampoMasivo.Unidad:
            {
                var unidad = UnidadExistente(solicitud.Unidad);
                var cambiados = productos.CambiarUnidad(ids, unidad, Ahora());
                return new ResultadoMasivo(cambiados, CampoMasivo.Unidad, unidad);
            }
            default:
                throw new ReglaNegocioException("En bloque solo se puede cambiar la categoría o la unidad.");
        }
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

        var unidad = UnidadExistente(Texto.Mayusculas(datos.Unidad) is { Length: > 0 } u ? u : Unidad.Predeterminada);

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

    /// <summary>La unidad debe estar en la lista administrable; devuelve su nombre tal como está guardado.</summary>
    private string UnidadExistente(string? nombre)
    {
        var limpio = Texto.Mayusculas(nombre);
        if (limpio.Length == 0) throw new ReglaNegocioException("Seleccione una unidad de medida.");
        return unidades.ObtenerPorNombre(limpio)?.Nombre
            ?? throw new ReglaNegocioException($"La unidad '{limpio}' no existe. Agréguela en Productos > Unidades.");
    }

    private string NombreUnidad(string? nombre, long? excluirId)
    {
        var limpio = Texto.Mayusculas(nombre);
        if (limpio.Length == 0) throw new ReglaNegocioException("Indique el nombre de la unidad (ej. METRO, YARDA).");
        if (limpio.Length > 12) throw new ReglaNegocioException("El nombre de la unidad admite máximo 12 caracteres.");
        if (unidades.ExisteNombre(limpio, excluirId))
            throw new ReglaNegocioException($"Ya existe la unidad '{limpio}'.");
        return limpio;
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
