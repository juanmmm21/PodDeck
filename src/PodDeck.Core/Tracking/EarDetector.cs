using PodDeck.Core.Devices;

namespace PodDeck.Core.Tracking;

/// <summary>Cambio confirmado en cuántos auriculares hay puestos: de <see cref="Before"/> a <see cref="After"/>.</summary>
public readonly record struct EarChange(int Before, int After)
{
    public bool IsRemoval => After < Before;
}

/// <summary>
/// Convierte los estados del tracker en sucesos «se ha quitado / puesto un auricular». No es seguro
/// entre hilos, igual que <see cref="AirPodsTracker"/>.
/// </summary>
/// <remarks>
/// Cuenta los auriculares en la oreja en vez de mirar cada uno, porque al quitarse uno el que emite
/// puede pasar a ser el otro y cambia qué lado viene en qué nibble. Un cambio solo se da por bueno
/// cuando se mantiene <see cref="SettleTime"/>, para que un anuncio suelto no pause la música. Al
/// desaparecer los AirPods o cambiar de modelo se olvida la referencia: no hay nada que comparar.
/// </remarks>
public sealed class EarDetector
{
    public static readonly TimeSpan DefaultSettleTime = TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _time;
    private ushort? _productId;
    private int? _pendingCount;
    private DateTimeOffset _pendingSince;

    public EarDetector(TimeProvider time, TimeSpan? settleTime = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        SettleTime = settleTime ?? DefaultSettleTime;
        if (SettleTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settleTime), settleTime, "SettleTime cannot be negative.");
        }
    }

    public TimeSpan SettleTime { get; }

    /// <summary>Auriculares puestos según el último cambio confirmado, o null si no hay AirPods.</summary>
    public int? InEarCount { get; private set; }

    /// <summary>
    /// Hay que llamarlo con cada estado nuevo del tracker y también periódicamente, porque el cambio
    /// se confirma con el paso del tiempo aunque no llegue ningún estado distinto.
    /// </summary>
    public EarChange? Update(AirPodsStatus? current)
    {
        if (current is null || current.ProductId != _productId)
        {
            _productId = current?.ProductId;
            InEarCount = current is null ? null : CountInEar(current);
            _pendingCount = null;
            return null;
        }

        int count = CountInEar(current);
        DateTimeOffset now = _time.GetUtcNow();
        if (count == InEarCount)
        {
            _pendingCount = null;
            return null;
        }

        if (count != _pendingCount)
        {
            _pendingCount = count;
            _pendingSince = now;
        }

        if (now - _pendingSince < SettleTime)
        {
            return null;
        }

        var change = new EarChange(InEarCount ?? 0, count);
        InEarCount = count;
        _pendingCount = null;
        return change;
    }

    private static int CountInEar(AirPodsStatus status) =>
        (status.Left.IsInEar ? 1 : 0) + (status.Right.IsInEar ? 1 : 0);
}
