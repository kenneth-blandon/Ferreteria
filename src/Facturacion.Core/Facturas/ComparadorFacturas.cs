using Facturacion.Core.Comun;

namespace Facturacion.Core.Facturas;

/// <summary>
/// Compara una factura emitida con su versión corregida y describe los cambios en lenguaje sencillo
/// ("CABLE THHN: cantidad 25 → 30"). Una lista vacía significa que no hubo cambios.
/// </summary>
public static class ComparadorFacturas
{
    public static IReadOnlyList<string> Diferencias(Factura anterior, Factura nueva)
    {
        var cambios = new List<string>();

        Comparar(cambios, "Cliente", anterior.ClienteNombre, nueva.ClienteNombre);
        Comparar(cambios, "RUC/cédula", anterior.ClienteRuc, nueva.ClienteRuc);
        Comparar(cambios, "Dirección", anterior.ClienteDireccion, nueva.ClienteDireccion);

        var antes = Agrupar(anterior.Lineas);
        var despues = Agrupar(nueva.Lineas);
        foreach (var (clave, a) in antes)
        {
            if (!despues.TryGetValue(clave, out var d))
            {
                cambios.Add($"Se quitó {a.Descripcion} ({Dinero.Cantidad(a.Cantidad)} {a.Unidad})");
                continue;
            }
            if (a.Cantidad != d.Cantidad)
                cambios.Add($"{a.Descripcion}: cantidad {Dinero.Cantidad(a.Cantidad)} → {Dinero.Cantidad(d.Cantidad)}");
            if (a.PrecioUnitario != d.PrecioUnitario)
                cambios.Add($"{a.Descripcion}: precio {Dinero.Formatear(a.PrecioUnitario)} → {Dinero.Formatear(d.PrecioUnitario)}");
        }
        foreach (var (clave, d) in despues)
        {
            if (!antes.ContainsKey(clave))
                cambios.Add($"Se agregó {d.Descripcion} ({Dinero.Cantidad(d.Cantidad)} {d.Unidad} × {Dinero.Formatear(d.PrecioUnitario)})");
        }

        if (anterior.MetodoPago != nueva.MetodoPago)
            cambios.Add($"Forma de pago: {anterior.MetodoPago} → {nueva.MetodoPago}");
        else if (nueva.MetodoPago == MetodoPago.Efectivo && anterior.MontoRecibido != nueva.MontoRecibido)
            cambios.Add($"Efectivo recibido: {Dinero.Formatear(anterior.MontoRecibido ?? 0)} → {Dinero.Formatear(nueva.MontoRecibido ?? 0)}");
        Comparar(cambios, "Referencia de pago", anterior.Referencia, nueva.Referencia);

        // El total se informa solo si algo más cambió (es consecuencia de las líneas o del IVA).
        if (cambios.Count > 0 && anterior.Total != nueva.Total)
            cambios.Add($"Total: {Dinero.Formatear(anterior.Total)} → {Dinero.Formatear(nueva.Total)}");
        return cambios;
    }

    private static void Comparar(List<string> cambios, string campo, string? antes, string? despues)
    {
        var a = antes ?? "";
        var d = despues ?? "";
        if (a != d) cambios.Add($"{campo}: {Mostrar(a)} → {Mostrar(d)}");
    }

    private static string Mostrar(string valor) => valor.Length == 0 ? "(vacío)" : valor;

    /// <summary>Agrupa por producto (o por descripción y unidad en artículos libres) sumando cantidades.</summary>
    private static Dictionary<string, LineaFactura> Agrupar(IEnumerable<LineaFactura> lineas)
    {
        var grupos = new Dictionary<string, LineaFactura>();
        foreach (var l in lineas)
        {
            var clave = l.ProductoId is { } id ? $"p{id}" : $"l{l.Descripcion}|{l.Unidad}";
            if (grupos.TryGetValue(clave, out var existente))
            {
                existente.Cantidad += l.Cantidad;
            }
            else
            {
                grupos[clave] = new LineaFactura
                {
                    ProductoId = l.ProductoId,
                    Descripcion = l.Descripcion,
                    Unidad = l.Unidad,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario
                };
            }
        }
        return grupos;
    }
}
