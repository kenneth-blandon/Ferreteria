namespace Facturacion.Web.Infraestructura;

/// <summary>Impresoras instaladas en el equipo (lo implementa el host de Windows).</summary>
public interface IImpresoras
{
    IReadOnlyList<string> Listar();
    string? Predeterminada();
}

/// <summary>Implementación vacía para el modo navegador: allí imprime el cuadro del navegador.</summary>
public sealed class SinImpresoras : IImpresoras
{
    public IReadOnlyList<string> Listar() => [];
    public string? Predeterminada() => null;
}
