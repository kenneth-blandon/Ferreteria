using Microsoft.Extensions.Logging;

namespace Facturacion.Web.Infraestructura;

/// <summary>
/// Guarda advertencias y errores en un archivo mensual (la versión de escritorio no tiene consola).
/// </summary>
public sealed class RegistroArchivoProvider(string carpeta) : ILoggerProvider
{
    private readonly Lock _bloqueo = new();

    public ILogger CreateLogger(string categoria) => new Registro(this, categoria);

    public void Dispose() { }

    private void Escribir(string texto)
    {
        try
        {
            lock (_bloqueo)
            {
                Directory.CreateDirectory(carpeta);
                File.AppendAllText(Path.Combine(carpeta, $"errores-{DateTime.Now:yyyy-MM}.log"), texto + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // El registro nunca debe detener la aplicación.
        }
    }

    private sealed class Registro(RegistroArchivoProvider proveedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Warning;

        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? excepcion, Func<TState, Exception?, string> formato)
        {
            if (!IsEnabled(nivel)) return;
            var detalle = excepcion is null ? "" : Environment.NewLine + excepcion;
            proveedor.Escribir($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{nivel}] {categoria}: {formato(estado, excepcion)}{detalle}");
        }
    }
}
