namespace Facturacion.Core.Comun;

public static class Reloj
{
    /// <summary>Hora local sin fracciones de segundo (la misma precisión con que se guarda).</summary>
    public static DateTime AhoraLocal(this TimeProvider reloj)
    {
        var ahora = reloj.GetLocalNow().DateTime;
        return ahora.AddTicks(-(ahora.Ticks % TimeSpan.TicksPerSecond));
    }
}
