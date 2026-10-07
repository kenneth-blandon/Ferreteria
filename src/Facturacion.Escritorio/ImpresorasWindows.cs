using System.Drawing.Printing;
using Facturacion.Web.Infraestructura;

namespace Facturacion.Escritorio;

/// <summary>Lista las impresoras instaladas en Windows para elegirlas en Configuración.</summary>
internal sealed class ImpresorasWindows : IImpresoras
{
    public IReadOnlyList<string> Listar()
    {
        try
        {
            return PrinterSettings.InstalledPrinters.Cast<string>().Order().ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public string? Predeterminada()
    {
        try
        {
            var ajustes = new PrinterSettings();
            return ajustes.IsDefaultPrinter ? ajustes.PrinterName : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
