using Dapper;
using Facturacion.Core.Configuracion;

namespace Facturacion.Datos.Repositorios;

public sealed class ConfiguracionRepositorio(ConexionSqlite db) : IConfiguracionRepositorio
{
    public string? Leer(string clave)
    {
        using var cn = db.Abrir();
        return cn.ExecuteScalar<string?>("SELECT valor FROM Configuracion WHERE clave = @clave", new { clave });
    }

    public void Escribir(string clave, string valor)
    {
        using var cn = db.Abrir();
        cn.Execute("""
            INSERT INTO Configuracion (clave, valor) VALUES (@clave, @valor)
            ON CONFLICT(clave) DO UPDATE SET valor = excluded.valor
            """, new { clave, valor });
    }
}
