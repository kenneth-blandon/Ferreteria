using Dapper;
using Facturacion.Core.Comun;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos.Respaldos;

/// <summary>
/// Copias de seguridad de la base de datos. Usa la API de respaldo de SQLite,
/// que copia de forma consistente aunque la aplicación esté abierta.
/// </summary>
public sealed class ServicioRespaldo(ConexionSqlite db, string carpetaRespaldos, TimeProvider reloj)
{
    public string CarpetaRespaldos { get; } = carpetaRespaldos;

    /// <summary>Copia la base de datos completa al archivo indicado.</summary>
    public void CrearEn(string rutaDestino)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rutaDestino))!);
        if (File.Exists(rutaDestino)) File.Delete(rutaDestino);

        using var origen = db.Abrir();
        using var destino = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = rutaDestino,
            Pooling = false
        }.ToString());
        destino.Open();
        origen.BackupDatabase(destino);
    }

    /// <summary>
    /// Un respaldo por día en la carpeta local de respaldos (se ejecuta al iniciar).
    /// Conserva los más recientes y borra los antiguos.
    /// </summary>
    public string? RespaldoDiario(int conservar = 30)
    {
        Directory.CreateDirectory(CarpetaRespaldos);
        var ruta = Path.Combine(CarpetaRespaldos, $"facturacion-{reloj.GetLocalNow():yyyy-MM-dd}.db");
        if (File.Exists(ruta)) return null;

        CrearEn(ruta);
        foreach (var viejo in Directory.GetFiles(CarpetaRespaldos, "facturacion-*.db")
                     .OrderByDescending(f => f)
                     .Skip(conservar))
        {
            try { File.Delete(viejo); } catch (IOException) { /* se intentará en el próximo inicio */ }
        }
        return ruta;
    }

    /// <summary>
    /// Reemplaza todos los datos actuales por los del respaldo indicado.
    /// Antes guarda una copia de seguridad del estado actual.
    /// </summary>
    public string Restaurar(string rutaRespaldo)
    {
        Validar(rutaRespaldo);

        var copiaPrevia = Path.Combine(CarpetaRespaldos, $"antes-de-restaurar-{reloj.GetLocalNow():yyyy-MM-dd-HHmmss}.db");
        CrearEn(copiaPrevia);

        using (var origen = new SqliteConnection(new SqliteConnectionStringBuilder
               {
                   DataSource = rutaRespaldo,
                   Mode = SqliteOpenMode.ReadOnly,
                   Pooling = false
               }.ToString()))
        using (var destino = db.Abrir())
        {
            origen.Open();
            origen.BackupDatabase(destino);
        }

        Migraciones.Aplicar(db);
        return copiaPrevia;
    }

    private static void Validar(string ruta)
    {
        try
        {
            using var cn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = ruta,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            cn.Open();

            var tablas = cn.ExecuteScalar<int>("""
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name IN ('Configuracion', 'Productos', 'Facturas', 'DetalleFactura')
                """);
            if (tablas != 4)
                throw new ReglaNegocioException("El archivo no es un respaldo de este sistema de facturación.");

            var version = cn.ExecuteScalar<int>("PRAGMA user_version");
            if (version > Migraciones.VersionActual)
                throw new ReglaNegocioException("El respaldo es de una versión más nueva del sistema. Actualice la aplicación primero.");

            if (cn.ExecuteScalar<string>("PRAGMA quick_check") != "ok")
                throw new ReglaNegocioException("El archivo de respaldo está dañado.");
        }
        catch (SqliteException)
        {
            throw new ReglaNegocioException("El archivo no es una base de datos válida.");
        }
    }
}
