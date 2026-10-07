using Facturacion.Web;

// Modo navegador: útil para desarrollar y probar. Abra la dirección que se muestra en la consola.
// Cambiar carpeta de datos: dotnet run -- --Datos:Carpeta=C:\ruta
var app = AplicacionWeb.Crear(args, new OpcionesAplicacion
{
    Url = "http://127.0.0.1:5080",
    NombreHost = "Servidor"
});

app.Run();
