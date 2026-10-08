using Facturacion.Core.Comun;
using Facturacion.Core.Importacion;

namespace Facturacion.Core.Catalogo;

public interface IProductoRepositorio
{
    Producto? Obtener(long id);

    /// <summary>Busca por código interno o código de barras exacto (sin importar mayúsculas).</summary>
    Producto? ObtenerPorCodigo(string codigo);

    IReadOnlyList<Producto> ObtenerVarios(IReadOnlyCollection<long> ids);

    /// <summary>
    /// Autocompletado: todos los términos deben aparecer (en nombre, código, barras o categoría).
    /// Ordena primero coincidencias exactas de código y luego por nombre.
    /// </summary>
    IReadOnlyList<Producto> Buscar(IReadOnlyList<string> terminos, string consultaExacta, int limite);

    Pagina<Producto> Listar(FiltroProductos filtro, IReadOnlyList<string> terminos);

    IReadOnlyList<Producto> ListarTodos();

    IReadOnlyList<Producto> BajoStock();

    bool ExisteCodigo(string codigo, long? excluirId = null);

    bool ExisteCodigoBarras(string codigoBarras, long? excluirId = null);

    /// <summary>Próximo código automático libre: P00001, P00002...</summary>
    string SiguienteCodigoAutomatico();

    /// <summary>Inserta el producto; si trae existencias registra el movimiento inicial.</summary>
    long Crear(Producto producto, DateTime fecha);

    /// <summary>Actualiza datos generales. Las existencias solo cambian por movimientos.</summary>
    void Actualizar(Producto producto, DateTime fecha);

    void ActualizarPrecio(long id, decimal precio, DateTime fecha);

    void Eliminar(long id);

    /// <summary>Carga masiva (UPSERT por código) dentro de una sola transacción.</summary>
    ResultadoCarga CargarMasivo(IReadOnlyList<FilaProducto> filas, DateTime fecha);

    int ContarExistentes(IReadOnlyCollection<long> ids);

    /// <summary>Asigna la categoría (o ninguna) a varios productos en una transacción.</summary>
    int CambiarCategoria(IReadOnlyCollection<long> ids, long? categoriaId, DateTime fecha);

    /// <summary>Asigna la unidad a varios productos en una transacción.</summary>
    int CambiarUnidad(IReadOnlyCollection<long> ids, string unidad, DateTime fecha);
}

public interface IUnidadRepositorio
{
    IReadOnlyList<Unidad> Listar();
    Unidad? Obtener(long id);
    Unidad? ObtenerPorNombre(string nombre);
    bool ExisteNombre(string nombre, long? excluirId = null);
    long Crear(string nombre);
    /// <summary>Renombra la unidad y la actualiza en los productos que la usan.</summary>
    void Renombrar(long id, string nombre);
    void Eliminar(long id);
}

public interface ICategoriaRepositorio
{
    IReadOnlyList<Categoria> Listar();
    Categoria? Obtener(long id);
    bool ExisteNombre(string nombre, long? excluirId = null);
    long Crear(string nombre);
    /// <summary>Renombra y recalcula el texto de búsqueda de sus productos.</summary>
    void Renombrar(long id, string nombre);
    /// <summary>Los productos de la categoría quedan sin categoría.</summary>
    void Eliminar(long id);
}
