using System.Text;
using Facturacion.Core.Catalogo;
using Facturacion.Core.Comun;
using Facturacion.Core.Inventario;
using Microsoft.Data.Sqlite;

namespace Facturacion.Tests;

public class CatalogoTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    [Fact]
    public void Normaliza_y_evita_codigos_duplicados_sin_importar_mayusculas()
    {
        var p = e.Catalogo.Crear(new DatosProducto(" prod001 ", null, "  tubo   pvc ", null, "", 10.005m, 0));
        Assert.Equal("PROD001", p.Codigo);
        Assert.Equal("TUBO PVC", p.Nombre);
        Assert.Equal("UND", p.Unidad);
        Assert.Equal(10.01m, p.Precio);

        Assert.Throws<ReglaNegocioException>(() =>
            e.Catalogo.Crear(new DatosProducto("Prod001", null, "Otro", null, null, 1, 0)));
    }

    [Fact]
    public void Genera_codigo_automatico()
    {
        var a = e.Catalogo.Crear(new DatosProducto("", null, "Uno", null, null, 1, 0));
        var b = e.Catalogo.Crear(new DatosProducto(null, null, "Dos", null, null, 1, 0));
        Assert.Equal("P00001", a.Codigo);
        Assert.Equal("P00002", b.Codigo);
    }

    [Fact]
    public void Busca_sin_tildes_por_varias_palabras_y_prioriza_codigo_exacto()
    {
        var categoria = e.Catalogo.Categorias().Single(c => c.Nombre == "FONTANERÍA");
        e.CrearProducto("FON-034", "Tubería PVC 3/4 x 6m", 120m, categoriaId: categoria.Id);
        e.CrearProducto("FON-012", "Tubería PVC 1/2 x 6m", 90m, categoriaId: categoria.Id);
        e.CrearProducto("ELE-034", "Tubo EMT 3/4", 150m);

        Assert.Equal(["FON-034"], e.Catalogo.Buscar("tuberia 3/4").Select(p => p.Codigo));
        Assert.Equal(2, e.Catalogo.Buscar("fontaneria").Count);
        Assert.Equal("ELE-034", e.Catalogo.Buscar("ele-034")[0].Codigo);
        Assert.Empty(e.Catalogo.Buscar("t"));
    }

    [Fact]
    public void Limita_resultados_de_busqueda()
    {
        for (var i = 0; i < 60; i++) e.CrearProducto($"C-{i:00}", $"Clavo {i}", 1m);
        Assert.Equal(20, e.Catalogo.Buscar("clavo").Count);
        Assert.Equal(50, e.Catalogo.Buscar("clavo", 1000).Count);
        Assert.Equal(60, e.Catalogo.Listar(new FiltroProductos("clavo")).Total);
    }

    [Fact]
    public void Busca_por_codigo_de_barras_exacto()
    {
        var p = e.Catalogo.Crear(new DatosProducto("HER-1", "7501234567890", "Martillo", null, null, 320m, 0));
        Assert.Equal(p.Id, e.Catalogo.ObtenerPorCodigo("7501234567890").Id);
        Assert.Equal(p.Id, e.Catalogo.ObtenerPorCodigo("her-1").Id);
        Assert.Throws<NoEncontradoException>(() => e.Catalogo.ObtenerPorCodigo("NOEXISTE"));
    }

    [Fact]
    public void Renombrar_categoria_actualiza_la_busqueda()
    {
        var categoria = e.Catalogo.CrearCategoria("Pinturas");
        e.CrearProducto("PIN-1", "Galón blanco", 500m, categoriaId: categoria.Id);
        e.Catalogo.RenombrarCategoria(categoria.Id, "Acabados");

        Assert.Single(e.Catalogo.Buscar("acabados"));
        Assert.Empty(e.Catalogo.Buscar("pinturas"));
        Assert.Equal("ACABADOS", e.Catalogo.Categorias().Single(c => c.Id == categoria.Id).Nombre);
    }

    [Fact]
    public void Lista_paginada_y_filtra_bajo_stock()
    {
        e.CrearProducto("A", "Alfa", 1m, stock: 1, minimo: 5);
        e.CrearProducto("B", "Beta", 1m, stock: 50, minimo: 5);
        e.CrearProducto("C", "Gamma", 1m, stock: 0);

        var bajo = e.Catalogo.Listar(new FiltroProductos(SoloBajoStock: true));
        Assert.Equal("A", Assert.Single(bajo.Elementos).Codigo);
        Assert.True(bajo.Elementos[0].BajoStock);
        Assert.Equal(3, e.Catalogo.Listar(new FiltroProductos()).Total);
        Assert.Single(e.Inventario.Alertas());
    }

    [Fact]
    public void Ajustes_de_stock_registran_movimientos()
    {
        var p = e.CrearProducto("A", "Alfa", 1m, stock: 10);
        e.Inventario.Ajustar(p.Id, new SolicitudAjuste(TipoAjuste.Entrada, 5, "Compra"));
        e.Inventario.Ajustar(p.Id, new SolicitudAjuste(TipoAjuste.Salida, 2.5m, "Dañado"));
        var conteo = e.Inventario.Ajustar(p.Id, new SolicitudAjuste(TipoAjuste.Conteo, 11, null));

        Assert.Equal(11m, e.Catalogo.Obtener(p.Id).Stock);
        Assert.Equal(-1.5m, conteo.Cantidad);
        Assert.Equal(4, e.Inventario.Movimientos(p.Id).Count);
        Assert.Throws<ReglaNegocioException>(() => e.Inventario.Ajustar(p.Id, new SolicitudAjuste(TipoAjuste.Entrada, 0, null)));
    }

    [Fact]
    public void Eliminar_producto_conserva_las_facturas()
    {
        var p = e.CrearProducto("A", "Alfa", 10m);
        var factura = e.Facturacion.Emitir(Entorno.Venta(Entorno.Linea(p, 1)));
        e.Catalogo.Eliminar(p.Id);

        var leida = e.Facturacion.Obtener(factura.Id);
        Assert.Null(leida.Lineas[0].ProductoId);
        Assert.Equal("ALFA", leida.Lineas[0].Descripcion);
        // Anular una factura cuyo producto ya no existe no debe fallar.
        e.Facturacion.Anular(factura.Id, "Prueba");
    }
}

public class ImportacionTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    private static MemoryStream Csv(string contenido, Encoding? codificacion = null) =>
        new((codificacion ?? Encoding.UTF8).GetBytes(contenido));

    [Fact]
    public void Inserta_actualiza_y_reporta_errores()
    {
        e.CrearProducto("ELE-001", "Cable viejo", 10m, stock: 7);

        var resultado = e.Importacion.Importar(Csv("""
            Código;Descripción;Precio;Existencia;Categoría;Unidad;Stock mínimo
            ele-001;Cable THHN #12;28,50;500;Eléctricos;MTS;100
            FON-001;Tubo PVC 1/2;95;40;Plomería;UND;
            FON-002;Sin precio;abc;1;;;
            fon-001;Duplicado;1;1;;;
            HER-001;Martillo;320;;;;
            """), "productos.csv");

        Assert.Equal(5, resultado.FilasLeidas);
        Assert.Equal(2, resultado.Insertados);
        Assert.Equal(1, resultado.Actualizados);
        Assert.Equal([4, 5], resultado.Errores.Select(x => x.Fila));

        var cable = e.Catalogo.ObtenerPorCodigo("ELE-001");
        Assert.Equal(("CABLE THHN #12", 28.50m, 500m, "MTS", 100m), (cable.Nombre, cable.Precio, cable.Stock, cable.Unidad, cable.StockMinimo));
        Assert.Equal("ELÉCTRICOS", cable.Categoria);
        Assert.Equal("PLOMERÍA", e.Catalogo.ObtenerPorCodigo("FON-001").Categoria);
        Assert.Equal(0m, e.Catalogo.ObtenerPorCodigo("HER-001").Stock);
        Assert.Contains(e.Inventario.Movimientos(cable.Id), m => m.Tipo == TipoMovimiento.Importacion && m.Cantidad == 493m);
    }

    [Fact]
    public void Celdas_vacias_conservan_valores_actuales()
    {
        var categoria = e.Catalogo.Categorias().Single(c => c.Nombre == "HERRAMIENTAS");
        Assert.Throws<ReglaNegocioException>(() => e.Catalogo.CrearCategoria("herramientas"));
        e.CrearProducto("HER-001", "Martillo", 300m, stock: 12, minimo: 3, categoriaId: categoria.Id);

        e.Importacion.Importar(Csv("codigo,nombre,precio,stock,categoria\nHER-001,Martillo 16oz,320,,\n"), "a.csv");

        var p = e.Catalogo.ObtenerPorCodigo("HER-001");
        Assert.Equal((320m, 12m, 3m, "HERRAMIENTAS"), (p.Precio, p.Stock, p.StockMinimo, p.Categoria));
        Assert.Single(e.Catalogo.Buscar("herramientas 16oz"));
    }

    [Fact]
    public void Lee_csv_en_ansi_con_comillas()
    {
        var contenido = "Codigo;Nombre;Precio\r\n\"X-1\";\"Tubería \"\"especial\"\"; 3/4\";\"1.250,50\"\r\n";
        var resultado = e.Importacion.Importar(Csv(contenido, Encoding.Latin1), "ansi.csv");

        Assert.Empty(resultado.Errores);
        var p = e.Catalogo.ObtenerPorCodigo("X-1");
        Assert.Equal("TUBERÍA \"ESPECIAL\"; 3/4", p.Nombre);
        Assert.Equal(1250.50m, p.Precio);
    }

    [Fact]
    public void Exige_columnas_obligatorias_y_formato_conocido()
    {
        var error = Assert.Throws<ReglaNegocioException>(() => e.Importacion.Importar(Csv("codigo,nombre\nA,B\n"), "a.csv"));
        Assert.Contains("precio", error.Message);
        Assert.Throws<ReglaNegocioException>(() => e.Importacion.Importar(Csv("x"), "a.pdf"));
    }

    [Fact]
    public void Exporta_a_excel_y_se_puede_volver_a_importar()
    {
        e.CrearProducto("A-1", "Alfa", 10.5m, stock: 3);
        e.CrearProducto("B-1", "Beta", 20m, stock: 4);
        var excel = e.Importacion.Exportar();

        using var otro = new Entorno();
        var resultado = otro.Importacion.Importar(new MemoryStream(excel), "inventario.xlsx");

        Assert.Empty(resultado.Errores);
        Assert.Equal(2, resultado.Insertados);
        Assert.Equal(10.5m, otro.Catalogo.ObtenerPorCodigo("A-1").Precio);
        Assert.Equal(4m, otro.Catalogo.ObtenerPorCodigo("B-1").Stock);

        // La plantilla también se puede importar tal cual (sus filas de ejemplo son válidas).
        var deLaPlantilla = otro.Importacion.Importar(new MemoryStream(e.Importacion.Plantilla()), "plantilla.xlsx");
        Assert.Empty(deLaPlantilla.Errores);
        Assert.Equal(3, deLaPlantilla.Insertados);
        Assert.Equal("7501234567890", otro.Catalogo.ObtenerPorCodigo("HER-001").CodigoBarras);
    }

    [Fact]
    public void Codigo_de_barras_repetido_en_la_base_se_reporta_por_fila()
    {
        e.Catalogo.Crear(new DatosProducto("A-1", "111", "Alfa", null, null, 1, 0));
        var resultado = e.Importacion.Importar(Csv("codigo,nombre,precio,codigo de barras\nB-1,Beta,2,111\nC-1,Gamma,3,222\n"), "a.csv");

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(2, Assert.Single(resultado.Errores).Fila);
    }
}

public class RespaldoTests : IDisposable
{
    private readonly Entorno e = new();
    public void Dispose() => e.Dispose();

    [Fact]
    public void Respalda_y_restaura()
    {
        e.CrearProducto("A-1", "Alfa", 10m);
        var respaldo = Path.Combine(e.Carpeta, "copia.db");
        e.Respaldo.CrearEn(respaldo);

        e.CrearProducto("B-1", "Beta", 10m);
        Assert.Equal(2, e.Catalogo.Listar(new FiltroProductos()).Total);

        e.Respaldo.Restaurar(respaldo);
        Assert.Equal(1, e.Catalogo.Listar(new FiltroProductos()).Total);
        Assert.Single(Directory.GetFiles(e.Respaldo.CarpetaRespaldos, "antes-de-restaurar-*.db"));
    }

    [Fact]
    public void Respaldo_diario_una_vez_por_dia()
    {
        Assert.NotNull(e.Respaldo.RespaldoDiario());
        Assert.Null(e.Respaldo.RespaldoDiario());
        e.Reloj.Ahora = e.Reloj.Ahora.AddDays(1);
        Assert.NotNull(e.Respaldo.RespaldoDiario());
    }

    [Fact]
    public void Rechaza_archivos_que_no_son_respaldos()
    {
        var falso = Path.Combine(e.Carpeta, "falso.db");
        File.WriteAllText(falso, "esto no es una base de datos");
        Assert.Throws<ReglaNegocioException>(() => e.Respaldo.Restaurar(falso));

        var vacio = Path.Combine(e.Carpeta, "vacio.db");
        using (var cn = new SqliteConnection($"Data Source={vacio};Pooling=False"))
        {
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "CREATE TABLE X (a INTEGER)";
            cmd.ExecuteNonQuery();
        }
        Assert.Throws<ReglaNegocioException>(() => e.Respaldo.Restaurar(vacio));
    }
}
