namespace Facturacion.Core.Comun;

/// <summary>Resultado paginado de un listado.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int Total, int NumeroPagina, int TamanoPagina)
{
    public int TotalPaginas => TamanoPagina <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));
}
