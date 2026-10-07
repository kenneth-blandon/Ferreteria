using Dapper;
using Facturacion.Core.Clientes;
using Facturacion.Core.Comun;

namespace Facturacion.Datos.Repositorios;

public sealed class ClienteRepositorio(ConexionSqlite db) : IClienteRepositorio
{
    private const string Consulta = "SELECT id, nombre, ruc, direccion, telefono, creado FROM Clientes";

    public Cliente? Obtener(long id)
    {
        using var cn = db.Abrir();
        return cn.QuerySingleOrDefault<Cliente>($"{Consulta} WHERE id = @id", new { id });
    }

    public IReadOnlyList<Cliente> Buscar(IReadOnlyList<string> terminos, int limite)
    {
        var parametros = new DynamicParameters();
        parametros.Add("limite", limite);

        using var cn = db.Abrir();
        if (terminos.Count == 0)
            return cn.Query<Cliente>($"{Consulta} ORDER BY id DESC LIMIT @limite", parametros).ToList();

        var condiciones = new List<string>();
        for (var i = 0; i < terminos.Count; i++)
        {
            condiciones.Add($"busqueda LIKE @t{i} ESCAPE '\\'");
            parametros.Add($"t{i}", Sql.Contiene(terminos[i]));
        }
        return cn.Query<Cliente>(
            $"{Consulta} WHERE {string.Join(" AND ", condiciones)} ORDER BY nombre LIMIT @limite", parametros).ToList();
    }

    public bool ExisteNombre(string nombre, long? excluirId = null)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM Clientes WHERE nombre = @nombre AND (@excluirId IS NULL OR id <> @excluirId))",
            new { nombre, excluirId });
    }

    public long Crear(Cliente cliente)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<long>("""
            INSERT INTO Clientes (nombre, ruc, direccion, telefono, busqueda, creado)
            VALUES (@Nombre, @Ruc, @Direccion, @Telefono, @Busqueda, @Creado)
            RETURNING id
            """,
            new
            {
                cliente.Nombre,
                cliente.Ruc,
                cliente.Direccion,
                cliente.Telefono,
                Busqueda = Texto.ClaveBusqueda(cliente.Nombre, cliente.Ruc, cliente.Telefono),
                Creado = Sql.Fecha(cliente.Creado)
            });
    }

    public void Actualizar(Cliente cliente)
    {
        using var cn = db.Abrir();
        cn.Execute("""
            UPDATE Clientes
            SET nombre = @Nombre, ruc = @Ruc, direccion = @Direccion, telefono = @Telefono, busqueda = @Busqueda
            WHERE id = @Id
            """,
            new
            {
                cliente.Id,
                cliente.Nombre,
                cliente.Ruc,
                cliente.Direccion,
                cliente.Telefono,
                Busqueda = Texto.ClaveBusqueda(cliente.Nombre, cliente.Ruc, cliente.Telefono)
            });
    }

    public void Eliminar(long id)
    {
        using var cn = db.Abrir();
        cn.Execute("DELETE FROM Clientes WHERE id = @id", new { id });
    }
}
