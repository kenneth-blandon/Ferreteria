using Dapper;
using Facturacion.Core.Configuracion;
using Microsoft.Data.Sqlite;

namespace Facturacion.Datos;

/// <summary>
/// Datos con los que arranca una base nueva. Los del negocio se tomaron de la factura física
/// y se pueden corregir en Configuración.
/// </summary>
internal static class DatosIniciales
{
    private static readonly string[] Categorias = ["ELÉCTRICOS", "FONTANERÍA", "HERRAMIENTAS", "TORNILLERÍA"];

    public static void Insertar(SqliteConnection cn, SqliteTransaction tx)
    {
        foreach (var nombre in Categorias)
            cn.Execute("INSERT INTO Categorias (nombre) VALUES (@nombre)", new { nombre }, tx);

        var negocio = new DatosNegocio
        {
            NombreComercial = "Electric Hammer",
            Giro = "Materiales Eléctricos",
            Propietario = "Wilver Solís Rivera",
            Direccion = "Farmacia Cony 30 vrs. abajo, Mercado Roberto Huembes, Managua, Nicaragua",
            Telefono = "8632-6107 / 8458-8766",
            Email = "wilbersolis@yahoo.com",
            Ruc = "2411011840002F",
            MensajePie = "¡Gracias por su compra!",
            Condiciones = string.Join('\n',
                "1) Únicamente se autorizarán las devoluciones que hayan sido denunciadas dentro de un plazo de 10 días.",
                "2) El comprador da como bueno el valor total de esta factura y se compromete a cancelarlo al vencimiento acordado.",
                "3) La firma de cualquier empleado o dependiente del comprador, al aceptar esta factura, obliga a este a cumplir con todas las condiciones estipuladas en la misma.",
                "4) El comprador acepta como buenos los intereses por mora de esta factura.")
        };

        // Talonario de la foto: serie A, rango autorizado 39,001–44,000, formulario 40658.
        var facturacion = new AjustesFacturacion
        {
            Serie = "A",
            TipoFactura = "CONTADO",
            SiguienteNumero = 40658,
            RangoDesde = 39001,
            RangoHasta = 44000
        };

        Guardar(cn, tx, ClavesConfiguracion.Negocio, ServicioConfiguracion.ComoJson(negocio));
        Guardar(cn, tx, ClavesConfiguracion.Facturacion, ServicioConfiguracion.ComoJson(facturacion));
        Guardar(cn, tx, ClavesConfiguracion.Impresion, ServicioConfiguracion.ComoJson(new AjustesImpresion()));
        Guardar(cn, tx, ClavesConfiguracion.SiguienteNumero, facturacion.SiguienteNumero.ToString());
    }

    private static void Guardar(SqliteConnection cn, SqliteTransaction tx, string clave, string valor) =>
        cn.Execute("INSERT INTO Configuracion (clave, valor) VALUES (@clave, @valor)", new { clave, valor }, tx);
}
