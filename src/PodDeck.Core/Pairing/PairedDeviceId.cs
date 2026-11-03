using System.Globalization;
using PodDeck.Core.Protocol;

namespace PodDeck.Core.Pairing;

/// <summary>
/// Saca el producto de Apple de los identificadores de instancia que Windows da a los servicios
/// Bluetooth clásicos de un dispositivo emparejado, por ejemplo
/// <c>BTHENUM\{0000110B-...}_VID&amp;0001004C_PID&amp;2027\A&amp;1D440A10&amp;0&amp;74778624D7BB_C00000000</c>.
/// </summary>
/// <remarks>
/// El VID y el PID salen del perfil Device ID que el dispositivo publica al emparejarse: es el mismo
/// product id que luego viaja en el anuncio de proximidad, y por eso sirve para atar los dos mundos.
/// El primer dígito del VID es la fuente (1 = Bluetooth SIG); Apple es 004C.
/// </remarks>
public static class PairedDeviceId
{
    private const string VendorMarker = "_VID&";
    private const string ProductMarker = "_PID&";
    private const int VendorDigits = 8;
    private const int ProductDigits = 4;
    private const string BluetoothSigSource = "0001";

    public static bool TryParseAppleProductId(string? instanceId, out ushort productId)
    {
        productId = 0;
        if (string.IsNullOrEmpty(instanceId))
        {
            return false;
        }

        int vendorAt = instanceId.IndexOf(VendorMarker, StringComparison.OrdinalIgnoreCase);
        if (vendorAt < 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest = instanceId.AsSpan(vendorAt + VendorMarker.Length);
        if (rest.Length < VendorDigits + ProductMarker.Length + ProductDigits
            || !rest[..4].Equals(BluetoothSigSource, StringComparison.Ordinal)
            || !ushort.TryParse(rest[4..VendorDigits], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort vendor)
            || vendor != ProximityPairingParser.AppleCompanyId
            || !rest[VendorDigits..(VendorDigits + ProductMarker.Length)].Equals(ProductMarker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ReadOnlySpan<char> product = rest.Slice(VendorDigits + ProductMarker.Length, ProductDigits);
        return ushort.TryParse(product, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out productId);
    }
}
