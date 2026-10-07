using System.Text.Json;
using Facturacion.Core.Configuracion;
using Facturacion.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Facturacion.Escritorio;

/// <summary>Ventana principal: muestra la interfaz HTML y atiende los pedidos de impresión.</summary>
internal sealed class VentanaPrincipal : Form
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WebApplication _servidor;
    private readonly string _url;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private PuenteImpresion? _impresion;

    public VentanaPrincipal(WebApplication servidor, string url)
    {
        _servidor = servidor;
        _url = url;
        Text = "Facturación";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1000, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_web);
        Load += async (_, _) => await InicializarAsync();
    }

    private async Task InicializarAsync()
    {
        try
        {
            var info = _servidor.Services.GetRequiredService<InfoSistema>();
            var entorno = await CoreWebView2Environment.CreateAsync(null, Path.Combine(info.CarpetaDatos, "webview2"));
            await _web.EnsureCoreWebView2Async(entorno);

            var core = _web.CoreWebView2;
#if !DEBUG
            core.Settings.AreDevToolsEnabled = false;
#endif
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.DocumentTitleChanged += (_, _) => Text = $"{core.DocumentTitle}";
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.DownloadStarting += AlIniciarDescarga;
            core.WebMessageReceived += AlRecibirMensaje;

            _impresion = new PuenteImpresion(this, entorno, _servidor.Services.GetRequiredService<ServicioConfiguracion>());
            core.Navigate(_url);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                "Falta el componente Microsoft Edge WebView2 Runtime.\n\nDescárguelo e instálelo desde:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "Facturación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
        catch (Exception ex)
        {
            Program.MostrarError("No se pudo abrir la ventana", ex);
            Close();
        }
    }

    /// <summary>
    /// F10 y Alt+número son teclas especiales de Windows: se interceptan aquí y se envían a la página
    /// para que funcionen siempre los atajos (F10 = cobrar, Alt+1..6 = cambiar de módulo).
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        string? tecla = null;
        if (keyData == Keys.F10) tecla = "F10";
        else if ((keyData & Keys.Modifiers) == Keys.Alt && (keyData & Keys.KeyCode) is >= Keys.D1 and <= Keys.D6)
            tecla = $"Alt+{(keyData & Keys.KeyCode) - Keys.D0}";

        if (tecla is not null && _web.CoreWebView2 is not null)
        {
            Enviar(new { tipo = "tecla", tecla });
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private sealed record MensajePagina(long Id, string? Accion, string? Ruta, string? Archivo);

    private async void AlRecibirMensaje(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        MensajePagina? mensaje;
        try
        {
            mensaje = JsonSerializer.Deserialize<MensajePagina>(e.WebMessageAsJson, Json);
        }
        catch (JsonException)
        {
            return;
        }
        if (mensaje is null || _impresion is null) return;

        try
        {
            // Solo se imprimen documentos generados por la propia aplicación.
            if (mensaje.Ruta is null || !mensaje.Ruta.StartsWith("/imprimir/", StringComparison.Ordinal))
                throw new InvalidOperationException("Documento no válido.");
            var url = new Uri(new Uri(_url), mensaje.Ruta).ToString();

            switch (mensaje.Accion)
            {
                case "imprimir":
                    await _impresion.ImprimirAsync(url);
                    Responder(mensaje.Id, true, null);
                    break;
                case "pdf":
                    var archivo = await _impresion.GuardarPdfAsync(url, mensaje.Archivo);
                    Responder(mensaje.Id, true, archivo is null ? "Cancelado." : $"PDF guardado en {archivo}");
                    break;
                default:
                    throw new InvalidOperationException("Acción no reconocida.");
            }
        }
        catch (Exception ex)
        {
            Responder(mensaje.Id, false, ex.Message);
        }
    }

    private void Responder(long id, bool ok, string? texto) => Enviar(new { tipo = "respuesta", id, ok, mensaje = texto });

    private void Enviar(object datos) => _web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(datos, Json));

    /// <summary>Exportaciones y respaldos: se pregunta dónde guardar (memoria USB, Documentos...).</summary>
    private void AlIniciarDescarga(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        var diferido = e.GetDeferral();
        BeginInvoke(() =>
        {
            using (diferido)
            {
                var sugerido = e.ResultFilePath;
                var extension = Path.GetExtension(sugerido);
                using var dialogo = new SaveFileDialog
                {
                    FileName = Path.GetFileName(sugerido),
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Filter = extension switch
                    {
                        ".xlsx" => "Libro de Excel (*.xlsx)|*.xlsx",
                        ".db" => "Respaldo de base de datos (*.db)|*.db",
                        _ => "Todos los archivos (*.*)|*.*"
                    }
                };
                if (dialogo.ShowDialog(this) != DialogResult.OK)
                {
                    e.Cancel = true;
                    return;
                }
                e.ResultFilePath = dialogo.FileName;
                e.Handled = true;
                var destino = dialogo.FileName;
                e.DownloadOperation.StateChanged += (_, _) =>
                {
                    if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed)
                        Enviar(new { tipo = "descarga", ok = true, mensaje = $"Archivo guardado en {destino}" });
                    else if (e.DownloadOperation.State == CoreWebView2DownloadState.Interrupted)
                        Enviar(new { tipo = "descarga", ok = false, mensaje = "No se pudo guardar el archivo." });
                };
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _impresion?.Dispose();
        base.Dispose(disposing);
    }
}
