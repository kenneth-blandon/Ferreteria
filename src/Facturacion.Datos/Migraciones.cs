using Dapper;
using Facturacion.Core.Configuracion;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos;

/// <summary>
/// Crea y actualiza el esquema. La versión se guarda en PRAGMA user_version;
/// para un cambio futuro se agrega un paso nuevo al final de <see cref="Pasos"/>.
/// </summary>
public static class Migraciones
{
    private static readonly Action<SqliteConnection, SqliteTransaction>[] Pasos =
    [
        Version1,
        Version2
    ];

    public static int VersionActual => Pasos.Length;

    public static void Aplicar(ConexionSqlite db) => Aplicar(db, VersionActual);

    /// <summary>Aplica los pasos hasta la versión indicada (las pruebas lo usan para simular bases antiguas).</summary>
    internal static void Aplicar(ConexionSqlite db, int hastaVersion)
    {
        using var conexion = db.Abrir();
        var version = conexion.ExecuteScalar<int>("PRAGMA user_version");
        if (version > VersionActual)
            throw new InvalidOperationException(
                $"La base de datos es de una versión más nueva del sistema ({version}). Actualice la aplicación.");

        for (var paso = version; paso < hastaVersion; paso++)
        {
            using var transaccion = conexion.BeginTransaction();
            Pasos[paso](conexion, transaccion);
            conexion.Execute($"PRAGMA user_version = {paso + 1}", transaction: transaccion);
            transaccion.Commit();
        }
    }

    private static void Version1(SqliteConnection cn, SqliteTransaction tx)
    {
        cn.Execute("""
            CREATE TABLE Configuracion (
                clave TEXT PRIMARY KEY,
                valor TEXT NOT NULL
            );

            CREATE TABLE Categorias (
                id     INTEGER PRIMARY KEY AUTOINCREMENT,
                nombre TEXT NOT NULL COLLATE NOCASE UNIQUE
            );

            CREATE TABLE Productos (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                codigo        TEXT NOT NULL COLLATE NOCASE UNIQUE,
                codigo_barras TEXT COLLATE NOCASE,
                nombre        TEXT NOT NULL,
                categoria_id  INTEGER REFERENCES Categorias(id) ON DELETE SET NULL,
                unidad        TEXT NOT NULL DEFAULT 'UND',
                precio        REAL NOT NULL DEFAULT 0 CHECK (precio >= 0),
                stock         REAL NOT NULL DEFAULT 0,
                stock_minimo  REAL NOT NULL DEFAULT 0 CHECK (stock_minimo >= 0),
                busqueda      TEXT NOT NULL DEFAULT '',
                creado        TEXT NOT NULL,
                actualizado   TEXT NOT NULL
            );
            CREATE UNIQUE INDEX UX_Productos_CodigoBarras ON Productos(codigo_barras) WHERE codigo_barras IS NOT NULL;
            CREATE INDEX IX_Productos_Categoria ON Productos(categoria_id);
            CREATE INDEX IX_Productos_Nombre ON Productos(nombre);

            CREATE TABLE Clientes (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                nombre    TEXT NOT NULL COLLATE NOCASE UNIQUE,
                ruc       TEXT,
                direccion TEXT,
                telefono  TEXT,
                busqueda  TEXT NOT NULL DEFAULT '',
                creado    TEXT NOT NULL
            );

            CREATE TABLE Facturas (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                serie             TEXT NOT NULL,
                numero            INTEGER NOT NULL,
                fecha             TEXT NOT NULL,
                cliente_id        INTEGER REFERENCES Clientes(id) ON DELETE SET NULL,
                cliente_nombre    TEXT NOT NULL,
                cliente_ruc       TEXT,
                cliente_direccion TEXT,
                subtotal          REAL NOT NULL,
                iva               REAL NOT NULL,
                total             REAL NOT NULL,
                metodo_pago       TEXT NOT NULL,
                monto_recibido    REAL,
                vuelto            REAL,
                referencia        TEXT,
                estado            TEXT NOT NULL DEFAULT 'Emitida',
                motivo_anulacion  TEXT,
                fecha_anulacion   TEXT,
                UNIQUE (serie, numero)
            );
            CREATE INDEX IX_Facturas_Fecha ON Facturas(fecha);
            CREATE INDEX IX_Facturas_Cliente ON Facturas(cliente_id);

            CREATE TABLE DetalleFactura (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                factura_id      INTEGER NOT NULL REFERENCES Facturas(id) ON DELETE CASCADE,
                producto_id     INTEGER REFERENCES Productos(id) ON DELETE SET NULL,
                codigo          TEXT NOT NULL,
                descripcion     TEXT NOT NULL,
                unidad          TEXT NOT NULL,
                cantidad        REAL NOT NULL,
                precio_unitario REAL NOT NULL,
                total           REAL NOT NULL
            );
            CREATE INDEX IX_DetalleFactura_Factura ON DetalleFactura(factura_id);
            CREATE INDEX IX_DetalleFactura_Producto ON DetalleFactura(producto_id);

            CREATE TABLE MovimientosInventario (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                producto_id      INTEGER NOT NULL REFERENCES Productos(id) ON DELETE CASCADE,
                fecha            TEXT NOT NULL,
                tipo             TEXT NOT NULL,
                cantidad         REAL NOT NULL,
                stock_resultante REAL NOT NULL,
                referencia       TEXT,
                nota             TEXT
            );
            CREATE INDEX IX_Movimientos_Producto ON MovimientosInventario(producto_id, fecha);
            """, transaction: tx);

        DatosIniciales.Insertar(cn, tx);
    }

    /// <summary>
    /// Versión 2: modificación de facturas con constancia, unidades de medida administrables
    /// e impresión solo en talonario con las posiciones medidas sobre el formulario.
    /// </summary>
    private static void Version2(SqliteConnection cn, SqliteTransaction tx)
    {
        cn.Execute("""
            ALTER TABLE Facturas ADD COLUMN fecha_modificacion TEXT;

            CREATE TABLE ModificacionesFactura (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                factura_id       INTEGER NOT NULL REFERENCES Facturas(id) ON DELETE CASCADE,
                fecha            TEXT NOT NULL,
                empleado         TEXT NOT NULL,
                motivo           TEXT NOT NULL,
                cambios          TEXT NOT NULL,
                total_anterior   REAL NOT NULL,
                total_nuevo      REAL NOT NULL,
                datos_anteriores TEXT NOT NULL
            );
            CREATE INDEX IX_Modificaciones_Factura ON ModificacionesFactura(factura_id);

            CREATE TABLE Unidades (
                id     INTEGER PRIMARY KEY AUTOINCREMENT,
                nombre TEXT NOT NULL COLLATE NOCASE UNIQUE
            );
            """, transaction: tx);

        DatosIniciales.InsertarUnidades(cn, tx);
        // Las unidades que ya usan los productos se agregan a la lista.
        cn.Execute("INSERT OR IGNORE INTO Unidades (nombre) SELECT DISTINCT unidad FROM Productos WHERE TRIM(unidad) <> ''",
            transaction: tx);

        // Se conservan impresora, copias y desplazamientos; las posiciones pasan a las medidas del talonario.
        var json = cn.ExecuteScalar<string?>("SELECT valor FROM Configuracion WHERE clave = @clave",
            new { clave = ClavesConfiguracion.Impresion }, tx);
        var anterior = ServicioConfiguracion.DesdeJson<AjustesImpresion>(json);
        var nuevos = new AjustesImpresion
        {
            Impresora = anterior.Impresora,
            MostrarDialogo = anterior.MostrarDialogo,
            Copias = anterior.Copias,
            DesplazamientoXMm = anterior.DesplazamientoXMm,
            DesplazamientoYMm = anterior.DesplazamientoYMm
        };
        cn.Execute("""
            INSERT INTO Configuracion (clave, valor) VALUES (@clave, @valor)
            ON CONFLICT(clave) DO UPDATE SET valor = excluded.valor
            """, new { clave = ClavesConfiguracion.Impresion, valor = ServicioConfiguracion.ComoJson(nuevos) }, tx);
    }
}
