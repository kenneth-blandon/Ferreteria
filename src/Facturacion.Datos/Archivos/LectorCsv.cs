using System.Text;
using Facturacion.Core.Importacion;

namespace Facturacion.Datos.Archivos;

/// <summary>
/// Lee archivos CSV. Detecta el separador (coma, punto y coma o tabulador) y la codificación:
/// Excel en español guarda los CSV con ';' y en ANSI, por eso se prueban ambos casos.
/// </summary>
public sealed class LectorCsv : ILectorArchivo
{
    public IReadOnlyCollection<string> Extensiones { get; } = [".csv", ".txt"];

    public IReadOnlyList<FilaArchivo> Leer(Stream contenido)
    {
        var texto = LeerTexto(contenido);
        var registros = Separar(texto, DetectarSeparador(texto));
        if (registros.Count == 0) return [];

        var encabezados = registros[0].Campos.Select(ServicioImportacion.NormalizarEncabezado).ToList();
        var filas = new List<FilaArchivo>();
        foreach (var registro in registros.Skip(1))
        {
            var celdas = new Dictionary<string, string>();
            for (var i = 0; i < encabezados.Count; i++)
            {
                if (encabezados[i].Length == 0) continue;
                celdas.TryAdd(encabezados[i], i < registro.Campos.Count ? registro.Campos[i].Trim() : "");
            }
            filas.Add(new FilaArchivo(registro.Linea, celdas));
        }
        return filas;
    }

    private static string LeerTexto(Stream contenido)
    {
        using var memoria = new MemoryStream();
        contenido.CopyTo(memoria);
        var bytes = memoria.ToArray();

        try
        {
            // UTF-8 estricto: falla si el archivo viene en ANSI (Windows-1252).
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var inicio = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return utf8.GetString(bytes, inicio, bytes.Length - inicio);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static char DetectarSeparador(string texto)
    {
        var finPrimeraLinea = texto.IndexOfAny(['\r', '\n']);
        var encabezado = finPrimeraLinea < 0 ? texto : texto[..finPrimeraLinea];
        var candidatos = new[] { ';', ',', '\t' };
        return candidatos.OrderByDescending(c => encabezado.Count(x => x == c)).First();
    }

    private sealed record Registro(int Linea, List<string> Campos);

    /// <summary>Separa registros respetando comillas ("texto, con coma" y comillas dobles "").</summary>
    private static List<Registro> Separar(string texto, char separador)
    {
        var registros = new List<Registro>();
        var campos = new List<string>();
        var campo = new StringBuilder();
        var entreComillas = false;
        var linea = 1;
        var lineaInicio = 1;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreComillas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; }
                else if (c == '"') entreComillas = false;
                else
                {
                    if (c == '\n') linea++;
                    campo.Append(c);
                }
                continue;
            }

            if (c == '"') entreComillas = true;
            else if (c == separador) { campos.Add(campo.ToString()); campo.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                campos.Add(campo.ToString());
                campo.Clear();
                if (campos.Any(x => x.Length > 0)) registros.Add(new Registro(lineaInicio, campos));
                campos = [];
                linea++;
                lineaInicio = linea;
            }
            else campo.Append(c);
        }

        campos.Add(campo.ToString());
        if (campos.Any(x => x.Length > 0)) registros.Add(new Registro(lineaInicio, campos));
        return registros;
    }
}
