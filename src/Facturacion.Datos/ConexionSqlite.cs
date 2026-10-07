using Dapper;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos;

/// <summary>Fábrica de conexiones al archivo SQLite local.</summary>
public sealed class ConexionSqlite
{
    static ConexionSqlite()
    {
        // Permite mapear columnas snake_case (codigo_barras) a propiedades PascalCase (CodigoBarras).
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public ConexionSqlite(string rutaArchivo)
    {
        RutaArchivo = Path.GetFullPath(rutaArchivo);
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        CadenaConexion = new SqliteConnectionStringBuilder
        {
            DataSource = RutaArchivo,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true
        }.ToString();
    }

    public string RutaArchivo { get; }

    public string CadenaConexion { get; }

    public SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection(CadenaConexion);
        conexion.Open();
        return conexion;
    }
}
