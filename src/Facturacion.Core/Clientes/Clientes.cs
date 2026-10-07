using Facturacion.Core.Comun;
using Facturacion.Core.Facturas;

namespace Facturacion.Core.Clientes;

public sealed class Cliente
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Ruc { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public DateTime Creado { get; set; }
}

public sealed record DatosCliente(string? Nombre, string? Ruc, string? Direccion, string? Telefono);

public interface IClienteRepositorio
{
    Cliente? Obtener(long id);
    /// <summary>Sin términos devuelve los más recientes.</summary>
    IReadOnlyList<Cliente> Buscar(IReadOnlyList<string> terminos, int limite);
    bool ExisteNombre(string nombre, long? excluirId = null);
    long Crear(Cliente cliente);
    void Actualizar(Cliente cliente);
    /// <summary>Las facturas anteriores conservan su copia de los datos del cliente.</summary>
    void Eliminar(long id);
}

/// <summary>Clientes frecuentes (opcional: por defecto se factura a Consumidor Final).</summary>
public sealed class ServicioClientes(IClienteRepositorio clientes, TimeProvider reloj)
{
    public IReadOnlyList<Cliente> Buscar(string? texto, int limite = 20) =>
        clientes.Buscar(Texto.Terminos(texto), Math.Clamp(limite, 1, 200));

    public Cliente Obtener(long id) =>
        clientes.Obtener(id) ?? throw new NoEncontradoException("El cliente no existe.");

    public Cliente Crear(DatosCliente datos)
    {
        var cliente = Normalizar(datos, excluirId: null);
        cliente.Creado = reloj.AhoraLocal();
        cliente.Id = clientes.Crear(cliente);
        return cliente;
    }

    public Cliente Actualizar(long id, DatosCliente datos)
    {
        var actual = Obtener(id);
        var cliente = Normalizar(datos, excluirId: id);
        cliente.Id = id;
        cliente.Creado = actual.Creado;
        clientes.Actualizar(cliente);
        return cliente;
    }

    public void Eliminar(long id)
    {
        Obtener(id);
        clientes.Eliminar(id);
    }

    private Cliente Normalizar(DatosCliente datos, long? excluirId)
    {
        var nombre = Texto.Mayusculas(datos.Nombre);
        if (nombre.Length == 0) throw new ReglaNegocioException("El nombre del cliente es obligatorio.");
        if (nombre.Length > 120) throw new ReglaNegocioException("El nombre admite máximo 120 caracteres.");
        if (nombre == Factura.ConsumidorFinal)
            throw new ReglaNegocioException("'Consumidor final' ya existe por defecto; no hace falta registrarlo.");
        if (clientes.ExisteNombre(nombre, excluirId))
            throw new ReglaNegocioException($"Ya existe un cliente llamado '{nombre}'.");

        var ruc = Texto.MayusculasONulo(datos.Ruc);
        if (ruc is { Length: > 30 }) throw new ReglaNegocioException("El RUC/cédula admite máximo 30 caracteres.");
        var direccion = Texto.MayusculasONulo(datos.Direccion);
        if (direccion is { Length: > 200 }) throw new ReglaNegocioException("La dirección admite máximo 200 caracteres.");
        var telefono = Texto.LimpiarONulo(datos.Telefono);
        if (telefono is { Length: > 40 }) throw new ReglaNegocioException("El teléfono admite máximo 40 caracteres.");

        return new Cliente { Nombre = nombre, Ruc = ruc, Direccion = direccion, Telefono = telefono };
    }
}
