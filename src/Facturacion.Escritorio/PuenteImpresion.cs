using Facturacion.Core.Configuracion;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Facturacion.Escritorio;

/// <summary>
/// Imprime documentos HTML sin mostrar diálogos: los carga en un WebView2 oculto y los envía
/// a la impresora configurada (o la predeterminada de Windows). También genera PDF.
/// Los trabajos se atienden de uno en uno.
/// </summary>
internal sealed class PuenteImpresion : IDisposable
{
    private readonly Form _ventana;
    private readonly ServicioConfiguracion _configuracion;
    private readonly WebView2 _web;
    private readonly Task _listo;
    private readonly SemaphoreSlim _cola = new(1, 1);

    public PuenteImpresion(Form ventana, CoreWebView2Environment entorno, ServicioConfiguracion configuracion)
    {
        _ventana = ventana;
        _configuracion = configuracion;
        // Fuera de la pantalla pero "visible": así el motor procesa el documento normalmente.
        _web = new WebView2 { Location = new Point(-12000, -12000), Size = new Size(900, 1200), TabStop = false };
        ventana.Controls.Add(_web);
        _listo = _web.EnsureCoreWebView2Async(entorno);
    }

    public async Task ImprimirAsync(string url)
    {
        await _cola.WaitAsync();
        try
        {
            await CargarAsync(url);
            var ajustes = _configuracion.ObtenerImpresion();
            var core = _web.CoreWebView2;

            var opciones = core.Environment.CreatePrintSettings();
            opciones.ShouldPrintBackgrounds = true;
            opciones.ShouldPrintHeaderAndFooter = false;
            opciones.ScaleFactor = 1.0; // tamaño real: indispensable para el formato preimpreso
            opciones.MarginTop = opciones.MarginBottom = opciones.MarginLeft = opciones.MarginRight = 0;
            opciones.Copies = Math.Clamp(ajustes.Copias, 1, 5);
            if (!string.IsNullOrWhiteSpace(ajustes.Impresora)) opciones.PrinterName = ajustes.Impresora;

            var estado = await core.PrintAsync(opciones);
            if (estado == CoreWebView2PrintStatus.PrinterUnavailable)
                throw new InvalidOperationException(
                    $"La impresora {(string.IsNullOrWhiteSpace(ajustes.Impresora) ? "predeterminada" : $"'{ajustes.Impresora}'")} no está disponible. Revise que esté encendida y conectada.");
            if (estado != CoreWebView2PrintStatus.Succeeded)
                throw new InvalidOperationException("La impresora devolvió un error.");
        }
        finally
        {
            _cola.Release();
        }
    }

    /// <summary>Pide la ubicación y guarda el documento como PDF. Devuelve null si se cancela.</summary>
    public async Task<string?> GuardarPdfAsync(string url, string? nombreSugerido)
    {
        var nombre = string.Join("_", (nombreSugerido ?? "documento.pdf").Split(Path.GetInvalidFileNameChars()));
        using var dialogo = new SaveFileDialog
        {
            Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = nombre,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialogo.ShowDialog(_ventana) != DialogResult.OK) return null;

        await _cola.WaitAsync();
        try
        {
            await CargarAsync(url);
            var opciones = _web.CoreWebView2.Environment.CreatePrintSettings();
            opciones.ShouldPrintBackgrounds = true;
            opciones.ShouldPrintHeaderAndFooter = false;
            opciones.MarginTop = opciones.MarginBottom = opciones.MarginLeft = opciones.MarginRight = 0;
            if (!await _web.CoreWebView2.PrintToPdfAsync(dialogo.FileName, opciones))
                throw new InvalidOperationException("No se pudo crear el PDF.");
            return dialogo.FileName;
        }
        finally
        {
            _cola.Release();
        }
    }

    private async Task CargarAsync(string url)
    {
        await _listo;
        var cargado = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void AlCompletar(object? s, CoreWebView2NavigationCompletedEventArgs e) => cargado.TrySetResult(e.IsSuccess);

        var core = _web.CoreWebView2;
        core.NavigationCompleted += AlCompletar;
        try
        {
            core.Navigate(url);
            var primero = await Task.WhenAny(cargado.Task, Task.Delay(TimeSpan.FromSeconds(20)));
            if (primero != cargado.Task || !cargado.Task.Result)
                throw new InvalidOperationException("No se pudo preparar el documento para imprimir.");
        }
        finally
        {
            core.NavigationCompleted -= AlCompletar;
        }
    }

    public void Dispose()
    {
        _web.Dispose();
        _cola.Dispose();
    }
}
