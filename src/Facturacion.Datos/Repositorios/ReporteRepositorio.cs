using Dapper;
using Facturacion.Core.Comun;
using Facturacion.Core.Reportes;

namespace Facturacion.Datos.Repositorios;

public sealed class ReporteRepositorio(ConexionSqlite db) : IReporteRepositorio
{
    public ResumenCierre Cierre(DateOnly fecha)
    {
        var rango = new { desde = Sql.InicioDia(fecha), hasta = Sql.InicioDia(fecha.AddDays(1)) };
        using var cn = db.Abrir();

        var resumen = cn.QuerySingle<ResumenCierre>("""
            SELECT COALESCE(SUM(CASE WHEN estado = 'Emitida' THEN 1 ELSE 0 END), 0)       AS facturas_emitidas,
                   COALESCE(SUM(CASE WHEN estado = 'Anulada' THEN 1 ELSE 0 END), 0)       AS facturas_anuladas,
                   COALESCE(SUM(CASE WHEN estado = 'Emitida' THEN subtotal END), 0)       AS subtotal,
                   COALESCE(SUM(CASE WHEN estado = 'Emitida' THEN iva END), 0)            AS iva,
                   COALESCE(SUM(CASE WHEN estado = 'Emitida' THEN total END), 0)          AS total_vendido,
                   COALESCE(SUM(CASE WHEN estado = 'Anulada' THEN total END), 0)          AS total_anulado,
                   MIN(numero) AS primer_numero,
                   MAX(numero) AS ultimo_numero
            FROM Facturas
            WHERE fecha >= @desde AND fecha < @hasta
            """, rango);
        resumen.Fecha = fecha;

        resumen.PorMetodo = cn.Query<TotalPorMetodo>("""
            SELECT metodo_pago AS metodo, COUNT(*) AS facturas, SUM(total) AS total
            FROM Facturas
            WHERE estado = 'Emitida' AND fecha >= @desde AND fecha < @hasta
            GROUP BY metodo_pago
            """, rango).ToList();

        resumen.ProductosMasVendidos = cn.Query<ProductoVendido>("""
            SELECT d.codigo, d.descripcion, d.unidad, SUM(d.cantidad) AS cantidad, SUM(d.total) AS total
            FROM DetalleFactura d
            JOIN Facturas f ON f.id = d.factura_id
            WHERE f.estado = 'Emitida' AND f.fecha >= @desde AND f.fecha < @hasta
            GROUP BY d.codigo, d.descripcion, d.unidad
            ORDER BY total DESC
            LIMIT 10
            """, rango).ToList();

        // Los montos vienen como REAL: se redondean para evitar residuos de punto flotante.
        resumen.Subtotal = Dinero.Redondear(resumen.Subtotal);
        resumen.Iva = Dinero.Redondear(resumen.Iva);
        resumen.TotalVendido = Dinero.Redondear(resumen.TotalVendido);
        resumen.TotalAnulado = Dinero.Redondear(resumen.TotalAnulado);
        foreach (var m in resumen.PorMetodo) m.Total = Dinero.Redondear(m.Total);
        foreach (var p in resumen.ProductosMasVendidos)
        {
            p.Total = Dinero.Redondear(p.Total);
            p.Cantidad = Dinero.RedondearCantidad(p.Cantidad);
        }
        return resumen;
    }
}
