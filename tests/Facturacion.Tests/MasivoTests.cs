using Dapper;
using Facturacion.Core.Catalogo;
using Facturacion.Core.Comun;
using Facturacion.Core.Configuracion;
using Facturacion.Datos;
using Microsoft.Data.Sqlite;

namespace Facturacion.Tests;

public class MasivoTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    private long Categoria(string nombre) => e.Catalogo.Categorias().Single(c => c.Nombre == nombre).Id;

    [Fact]
    public void Cambia_la_categoria_de_los_seleccionados_y_su_busqueda()
    {
        var a = e.CrearProducto("A", "Tubo uno", 1m);
        var b = e.CrearProducto("B", "Tubo dos", 1m);
        var c = e.CrearProducto("C", "Tubo tres", 1m);

        var r = e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id, b.Id, b.Id], CampoMasivo.Categoria, Categoria("FONTANERÍA"), null));

        Assert.Equal((2, "FONTANERÍA"), (r.Actualizados, r.Valor));
        Assert.Equal("FONTANERÍA", e.Catalogo.Obtener(a.Id).Categoria);
        Assert.Null(e.Catalogo.Obtener(c.Id).Categoria);
        Assert.Equal(2, e.Catalogo.Buscar("fontaneria tubo").Count);

        e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id], CampoMasivo.Categoria, null, null));
        Assert.Null(e.Catalogo.Obtener(a.Id).Categoria);
        Assert.Single(e.Catalogo.Buscar("fontaneria"));
    }

    [Fact]
    public void Cambia_la_unidad_de_los_seleccionados()
    {
        var a = e.CrearProducto("A", "Cable", 1m);
        var b = e.CrearProducto("B", "Mecate", 1m);

        var r = e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id, b.Id], CampoMasivo.Unidad, null, "yarda"));

        Assert.Equal((2, "YARDA"), (r.Actualizados, r.Valor));
        Assert.Equal("YARDA", e.Catalogo.Obtener(b.Id).Unidad);
        Assert.Throws<ReglaNegocioException>(() =>
            e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id], CampoMasivo.Unidad, null, "NO EXISTE")));
    }

    [Fact]
    public void Valida_la_seleccion_y_el_campo()
    {
        var a = e.CrearProducto("A", "Alfa", 1m);
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.ActualizarMasivo(new SolicitudMasiva([], CampoMasivo.Unidad, null, "UND")));
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id, 999], CampoMasivo.Unidad, null, "UND")));
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id], CampoMasivo.Categoria, 999, null)));
        // Solo existen categoría y unidad: cualquier otro valor se rechaza.
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.ActualizarMasivo(new SolicitudMasiva([a.Id], (CampoMasivo)99, null, "UND")));
        Assert.Equal(["Categoria", "Unidad"], Enum.GetNames<CampoMasivo>());
    }
}

public class UnidadesTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    private Unidad Buscar(string nombre) => e.Catalogo.Unidades().Single(u => u.Nombre == nombre);

    [Fact]
    public void Trae_unidades_iniciales_y_permite_agregar()
    {
        var nombres = e.Catalogo.Unidades().Select(u => u.Nombre).ToList();
        Assert.Equal("UND", nombres[0]);
        Assert.Contains("METRO", nombres);
        Assert.Contains("YARDA", nombres);

        Assert.Equal("CAJA GRANDE", e.Catalogo.CrearUnidad("  caja   grande ").Nombre);
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.CrearUnidad("metro"));
    }

    [Fact]
    public void Renombrar_actualiza_los_productos()
    {
        var p = e.Catalogo.Crear(new DatosProducto("A", null, "Cable", null, "metro", 10m, 0));
        Assert.Equal("METRO", p.Unidad);

        e.Catalogo.RenombrarUnidad(Buscar("METRO").Id, "mts");

        Assert.Equal("MTS", e.Catalogo.Obtener(p.Id).Unidad);
        Assert.Equal(1, Buscar("MTS").TotalProductos);
    }

    [Fact]
    public void No_elimina_unidades_en_uso_ni_la_predeterminada()
    {
        e.Catalogo.Crear(new DatosProducto("A", null, "Cable", null, "METRO", 10m, 0));
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.EliminarUnidad(Buscar("METRO").Id));
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.EliminarUnidad(Buscar("UND").Id));
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.RenombrarUnidad(Buscar("UND").Id, "UNIDAD"));

        e.Catalogo.EliminarUnidad(Buscar("YARDA").Id);
        Assert.DoesNotContain(e.Catalogo.Unidades(), u => u.Nombre == "YARDA");
    }

    [Fact]
    public void Un_producto_solo_acepta_unidades_de_la_lista() =>
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.Crear(new DatosProducto("A", null, "Cable", null, "PALMO", 1m, 0)));

    [Fact]
    public void La_importacion_agrega_unidades_nuevas()
    {
        e.Importacion.Importar(new MemoryStream("codigo,nombre,precio,unidad\nA,Alfa,1,BULTO\n"u8.ToArray()), "a.csv");
        Assert.Equal(1, Buscar("BULTO").TotalProductos);
    }
}

public class MigracionTests : IDisposable
{
    private readonly string carpeta = Path.Combine(Path.GetTempPath(), "facturacion-pruebas", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(carpeta, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Una_base_version_1_se_actualiza_conservando_datos_y_calibracion()
    {
        var db = new ConexionSqlite(Path.Combine(carpeta, "v1.db"));
        Migraciones.Aplicar(db, hastaVersion: 1);
        using (var cn = db.Abrir())
        {
            cn.Execute("""
                INSERT INTO Productos (codigo, nombre, unidad, precio, stock, busqueda, creado, actualizado)
                VALUES ('A', 'CABLE', 'MTS', 10, 5, 'CABLE A', '2026-10-01 10:00:00', '2026-10-01 10:00:00');
                UPDATE Configuracion SET valor = '{"formato":"Ticket","impresora":"EPSON LX-350","copias":2,"anchoTicketMm":58,
                    "desplazamientoXMm":2.5,"desplazamientoYMm":-1,"plantilla":{"filas":{"y":67,"alto":6.6,"maximo":20}}}'
                WHERE clave = 'impresion';
                """);
        }

        Migraciones.Aplicar(db);

        using var conexion = db.Abrir();
        Assert.Equal(Migraciones.VersionActual, conexion.ExecuteScalar<int>("PRAGMA user_version"));
        var unidades = conexion.Query<string>("SELECT nombre FROM Unidades").ToList();
        Assert.Contains("MTS", unidades);
        Assert.Contains("YARDA", unidades);

        var impresion = ServicioConfiguracion.DesdeJson<AjustesImpresion>(
            conexion.ExecuteScalar<string>("SELECT valor FROM Configuracion WHERE clave = 'impresion'"));
        Assert.Equal(("EPSON LX-350", 2, 2.5m, -1m), (impresion.Impresora, impresion.Copias, impresion.DesplazamientoXMm, impresion.DesplazamientoYMm));
        Assert.Equal(22, impresion.Plantilla.Filas.Maximo);
        Assert.Equal(6.16m, impresion.Plantilla.Filas.Alto);
        Assert.Equal(0, conexion.ExecuteScalar<int>("SELECT COUNT(*) FROM Facturas WHERE fecha_modificacion IS NOT NULL"));
    }
}
