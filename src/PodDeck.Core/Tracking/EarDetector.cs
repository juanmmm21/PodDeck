using PodDeck.Core.Devices;

namespace PodDeck.Core.Tracking;

/// <summary>Cambio confirmado en cuántos auriculares hay puestos: de <see cref="Before"/> a <see cref="After"/>.</summary>
public readonly record struct EarChange(int Before, int After)
{
    public bool IsRemoval => After < Before;
}

/// <summary>
/// Convierte los anuncios de los AirPods seguidos en sucesos «se ha quitado / puesto un auricular».
/// No es seguro entre hilos, igual que <see cref="AirPodsTracker"/>.
/// </summary>
/// <remarks>
/// Cuenta los auriculares en la oreja en vez de mirar cada uno, porque al quitarse uno el que emite
/// puede pasar a ser el otro y cambia qué lado viene en qué nibble. Un cambio se da por bueno cuando
/// lo repiten <see cref="ConfirmationFrames"/> anuncios seguidos: así un anuncio suelto no pausa la
/// música, pero tampoco se espera un tiempo fijo (la primera versión esperaba medio segundo más el
/// tic del temporizador y se notaba lenta). Los dos auriculares emiten por turnos, así que el
/// segundo anuncio suele venir del otro y sirve de contraste.
/// </remarks>
public sealed class EarDetector
{
    public const int DefaultConfirmationFrames = 2;

    private ushort? _productId;
    private int? _pendingCount;
    private int _pendingFrames;

    public EarDetector(int confirmationFrames = DefaultConfirmationFrames)
    {
        if (confirmationFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmationFrames), confirmationFrames, "ConfirmationFrames must be at least 1.");
        }

        ConfirmationFrames = confirmationFrames;
    }

    public int ConfirmationFrames { get; }

    /// <summary>Auriculares puestos según el último cambio confirmado, o null si no hay referencia.</summary>
    public int? InEarCount { get; private set; }

    /// <summary>Hay que llamarlo con cada anuncio de los AirPods seguidos, aunque no cambie nada.</summary>
    public EarChange? Observe(AirPodsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        int count = CountInEar(status);
        if (status.ProductId != _productId || InEarCount is not int confirmed)
        {
            _productId = status.ProductId;
            InEarCount = count;
            _pendingCount = null;
            return null;
        }

        if (count == confirmed)
        {
            _pendingCount = null;
            return null;
        }

        if (count != _pendingCount)
        {
            _pendingCount = count;
            _pendingFrames = 0;
        }

        if (++_pendingFrames < ConfirmationFrames)
        {
            return null;
        }

        InEarCount = count;
        _pendingCount = null;
        return new EarChange(confirmed, count);
    }

    /// <summary>Los AirPods han desaparecido: no hay nada con lo que comparar el próximo anuncio.</summary>
    public void Reset()
    {
        _productId = null;
        InEarCount = null;
        _pendingCount = null;
    }

    private static int CountInEar(AirPodsStatus status) =>
        (status.Left.IsInEar ? 1 : 0) + (status.Right.IsInEar ? 1 : 0);
}
