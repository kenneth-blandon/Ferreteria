using System.Net;
using System.Net.Sockets;
using Facturacion.Web;
using Facturacion.Web.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Facturacion.Escritorio;

/// <summary>
/// Aplicación de escritorio: levanta el servidor local (solo accesible desde este equipo)
/// y lo muestra en una ventana de Windows con WebView2. No requiere internet.
/// </summary>
internal static class Program
{
    private const int PuertoPredeterminado = 47321;

    [STAThread]
    private static void Main(string[] args)
    {
        using var instancia = new Mutex(initiallyOwned: true, @"Local\FacturacionFerreteria", out var esLaPrimera);
        if (!esLaPrimera)
        {
            MessageBox.Show("La aplicación de facturación ya está abierta.", "Facturación",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MostrarError("Ocurrió un error inesperado", e.Exception);

        WebApplication servidor;
        string url;
        try
        {
            (servidor, url) = IniciarServidor(args);
        }
        catch (Exception ex)
        {
            MostrarError("No se pudo iniciar el sistema de facturación", ex);
            return;
        }

        try
        {
            Application.Run(new VentanaPrincipal(servidor, url));
        }
        finally
        {
            servidor.StopAsync().Wait(TimeSpan.FromSeconds(5));
            ((IDisposable)servidor).Dispose();
        }
    }

    /// <summary>Inicia el servidor en el puerto configurado o, si está ocupado, en el siguiente libre.</summary>
    private static (WebApplication Servidor, string Url) IniciarServidor(string[] args)
    {
        var configuracion = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddCommandLine(args)
            .Build();
        var puertoInicial = configuracion.GetValue("Servidor:Puerto", PuertoPredeterminado);

        for (var puerto = puertoInicial; puerto < puertoInicial + 10; puerto++)
        {
            if (!PuertoLibre(puerto)) continue;
            var url = $"http://127.0.0.1:{puerto}";
            var servidor = AplicacionWeb.Crear(args, new OpcionesAplicacion
            {
                Url = url,
                NombreHost = "Escritorio",
                ServiciosDelHost = s => s.AddSingleton<IImpresoras, ImpresorasWindows>()
            });
            servidor.StartAsync().GetAwaiter().GetResult();
            return (servidor, url);
        }
        throw new InvalidOperationException($"No hay puertos libres entre {puertoInicial} y {puertoInicial + 9}.");
    }

    private static bool PuertoLibre(int puerto)
    {
        try
        {
            var prueba = new TcpListener(IPAddress.Loopback, puerto);
            prueba.Start();
            prueba.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    internal static void MostrarError(string titulo, Exception ex) =>
        MessageBox.Show($"{titulo}.\n\n{ex.Message}", "Facturación", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
