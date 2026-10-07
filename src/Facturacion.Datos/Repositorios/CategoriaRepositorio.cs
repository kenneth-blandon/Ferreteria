using Dapper;
using Facturacion.Core.Catalogo;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos.Repositorios;

public sealed class CategoriaRepositorio(ConexionSqlite db) : ICategoriaRepositorio
{
    private const string Consulta = """
        SELECT c.id, c.nombre, COUNT(p.id) AS total_productos
        FROM Categorias c
        LEFT JOIN Productos p ON p.categoria_id = c.id
        """;

    public IReadOnlyList<Categoria> Listar()
    {
        using var cn = db.Abrir();
        return cn.Query<Categoria>($"{Consulta} GROUP BY c.id, c.nombre ORDER BY c.nombre").ToList();
    }

    public Categoria? Obtener(long id)
    {
        using var cn = db.Abrir();
        return cn.QuerySingleOrDefault<Categoria>($"{Consulta} WHERE c.id = @id GROUP BY c.id, c.nombre", new { id });
    }

    public bool ExisteNombre(string nombre, long? excluirId = null)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM Categorias WHERE nombre = @nombre AND (@excluirId IS NULL OR id <> @excluirId))",
            new { nombre, excluirId });
    }

    public long Crear(string nombre)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<long>("INSERT INTO Categorias (nombre) VALUES (@nombre) RETURNING id", new { nombre });
    }

    public void Renombrar(long id, string nombre)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();
        var afectados = ProductosDe(cn, tx, id);
        cn.Execute("UPDATE Categorias SET nombre = @nombre WHERE id = @id", new { id, nombre }, tx);
        RecalcularBusqueda(cn, tx, afectados);
        tx.Commit();
    }

    public void Eliminar(long id)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();
        var afectados = ProductosDe(cn, tx, id);
        cn.Execute("DELETE FROM Categorias WHERE id = @id", new { id }, tx);
        RecalcularBusqueda(cn, tx, afectados);
        tx.Commit();
    }

    private static List<ProductoBusqueda> ProductosDe(SqliteConnection cn, SqliteTransaction tx, long categoriaId) =>
        cn.Query<ProductoBusqueda>(
            "SELECT id, codigo, codigo_barras, nombre FROM Productos WHERE categoria_id = @categoriaId",
            new { categoriaId }, tx).ToList();

    /// <summary>El nombre de la categoría forma parte del texto de búsqueda del producto.</summary>
    private static void RecalcularBusqueda(SqliteConnection cn, SqliteTransaction tx, List<ProductoBusqueda> productos)
    {
        foreach (var p in productos)
        {
            var categoriaId = cn.ExecuteScalar<long?>("SELECT categoria_id FROM Productos WHERE id = @Id", new { p.Id }, tx);
            var busqueda = ProductoRepositorio.ClaveBusqueda(cn, tx, p.Nombre, p.Codigo, p.CodigoBarras, categoriaId);
            cn.Execute("UPDATE Productos SET busqueda = @busqueda WHERE id = @Id", new { busqueda, p.Id }, tx);
        }
    }

    private sealed class ProductoBusqueda
    {
        public long Id { get; set; }
        public string Codigo { get; set; } = "";
        public string? CodigoBarras { get; set; }
        public string Nombre { get; set; } = "";
    }
}
