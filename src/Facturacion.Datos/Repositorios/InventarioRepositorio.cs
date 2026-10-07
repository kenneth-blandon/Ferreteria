using Dapper;
using Facturacion.Core.Comun;
using Facturacion.Core.Inventario;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos.Repositorios;

public sealed class InventarioRepositorio(ConexionSqlite db) : IInventarioRepositorio
{
    public MovimientoInventario Ajustar(long productoId, TipoMovimiento tipo, Func<decimal, decimal> calcularNuevoStock, string? nota, DateTime fecha)
    {
        using var cn = db.Abrir();
        using var tx = cn.BeginTransaction();

        var actual = cn.ExecuteScalar<decimal?>("SELECT stock FROM Productos WHERE id = @productoId", new { productoId }, tx)
            ?? throw new NoEncontradoException("El producto no existe.");
        actual = Dinero.RedondearCantidad(actual);
        var nuevo = Dinero.RedondearCantidad(calcularNuevoStock(actual));

        cn.Execute("UPDATE Productos SET stock = @nuevo, actualizado = @fecha WHERE id = @productoId",
            new { productoId, nuevo, fecha = Sql.Fecha(fecha) }, tx);
        var movimiento = Registrar(cn, tx, productoId, fecha, tipo, nuevo - actual, nuevo, null, nota);

        tx.Commit();
        return movimiento;
    }

    public IReadOnlyList<MovimientoInventario> Movimientos(long productoId, int limite)
    {
        using var cn = db.Abrir();
        return cn.Query<MovimientoInventario>("""
            SELECT id, producto_id, fecha, tipo, cantidad, stock_resultante, referencia, nota
            FROM MovimientosInventario
            WHERE producto_id = @productoId
            ORDER BY fecha DESC, id DESC
            LIMIT @limite
            """, new { productoId, limite }).ToList();
    }

    /// <summary>Inserta un movimiento dentro de una transacción existente (ventas, anulaciones, importación).</summary>
    internal static MovimientoInventario Registrar(SqliteConnection cn, SqliteTransaction tx, long productoId, DateTime fecha,
        TipoMovimiento tipo, decimal cantidad, decimal stockResultante, string? referencia, string? nota)
    {
        var movimiento = new MovimientoInventario
        {
            ProductoId = productoId,
            Fecha = fecha,
            Tipo = tipo,
            Cantidad = Dinero.RedondearCantidad(cantidad),
            StockResultante = Dinero.RedondearCantidad(stockResultante),
            Referencia = referencia,
            Nota = nota
        };
        movimiento.Id = cn.ExecuteScalar<long>("""
            INSERT INTO MovimientosInventario (producto_id, fecha, tipo, cantidad, stock_resultante, referencia, nota)
            VALUES (@ProductoId, @Fecha, @Tipo, @Cantidad, @StockResultante, @Referencia, @Nota)
            RETURNING id
            """,
            new
            {
                movimiento.ProductoId,
                Fecha = Sql.Fecha(fecha),
                Tipo = tipo.ToString(),
                movimiento.Cantidad,
                movimiento.StockResultante,
                movimiento.Referencia,
                movimiento.Nota
            }, tx);
        return movimiento;
    }
}
