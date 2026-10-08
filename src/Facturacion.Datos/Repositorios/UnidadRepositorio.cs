using Dapper;
using Facturacion.Core.Catalogo;

namespace Facturacion.Datos.Repositorios;

public sealed class UnidadRepositorio(ConexionSqlite db) : IUnidadRepositorio
{
    private const string Consulta = """
        SELECT u.id, u.nombre,
               (SELECT COUNT(*) FROM Productos p WHERE p.unidad = u.nombre COLLATE NOCASE) AS total_productos
        FROM Unidades u
        """;

    public IReadOnlyList<Unidad> Listar()
    {
        using var cn = db.Abrir();
        return cn.Query<Unidad>($"{Consulta} ORDER BY u.nombre = 'UND' DESC, u.nombre").ToList();
    }

    public Unidad? Obtener(long id)
    {
        using var cn = db.Abrir();
        return cn.QuerySingleOrDefault<Unidad>($"{Consulta} WHERE u.id = @id", new { id });
    }

    public Unidad? ObtenerPorNombre(string nombre)
    {
        using var cn = db.Abrir();
        return cn.QuerySingleOrDefault<Unidad>($"{Consulta} WHERE u.nombre = @nombre", new { nombre });
    }

    public bool ExisteNombre(string nombre, long? excluirId = null)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM Unidades WHERE nombre = @nombre AND (@excluirId IS NULL OR id <> @excluirId))",
            new { nombre, excluirId });
    }

    public long Crear(string nombre)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<long>("INSERT INTO Unidades (nombre) VALUES (@nombre) RETURNING id", new { nombre });
    }

    public void Renombrar(long id, string nombre)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();
        var anterior = cn.ExecuteScalar<string>("SELECT nombre FROM Unidades WHERE id = @id", new { id }, tx);
        cn.Execute("UPDATE Unidades SET nombre = @nombre WHERE id = @id", new { id, nombre }, tx);
        cn.Execute("UPDATE Productos SET unidad = @nombre WHERE unidad = @anterior COLLATE NOCASE", new { nombre, anterior }, tx);
        tx.Commit();
    }

    public void Eliminar(long id)
    {
        using var cn = db.Abrir();
        cn.Execute("DELETE FROM Unidades WHERE id = @id", new { id });
    }
}
