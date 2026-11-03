using System.Buffers.Binary;
using PodDeck.Core.Devices;

namespace PodDeck.Core.Protocol;

/// <summary>
/// Decodifica el mensaje «proximity pairing» (tipo 0x07) que los AirPods emiten por BLE dentro de
/// los datos de fabricante de Apple (company id 0x004C). Es la misma señal con la que el iPhone
/// muestra la batería al abrir el estuche, y se recibe sin emparejar ni conectar nada.
/// </summary>
/// <remarks>
/// Apple no documenta el formato. La disposición sale de la ingeniería inversa publicada por
/// furiousMAC/continuity, OpenPods y AirPodsDesktop. Solo se leen los bytes en claro: el resto del
/// mensaje va cifrado con una clave que solo conocen los dispositivos emparejados.
/// </remarks>
public static class ProximityPairingParser
{
    public const ushort AppleCompanyId = 0x004C;

    private const byte ProximityPairingType = 0x07;
    private const int ClearTextLength = 10;

    private const int ProductIdOffset = 3;
    private const int StatusOffset = 5;
    private const int PodBatteriesOffset = 6;
    private const int ChargingAndCaseOffset = 7;
    private const int LidOffset = 8;

    private const byte PrimaryIsLeftFlag = 0x20;
    private const byte PrimaryInEarFlag = 0x02;
    private const byte SecondaryInEarFlag = 0x08;
    private const byte PrimaryInCaseFlag = 0x40;
    private const byte OnePodInCaseFlag = 0x10;
    private const byte BothPodsInCaseFlag = 0x04;
    private const byte LidClosedFlag = 0x08;

    private const int UnavailableNibble = 0x0F;
    private const int MaxBatteryNibble = 10;

    /// <summary>
    /// Busca y decodifica un mensaje de proximidad dentro de los datos de fabricante de Apple
    /// (sin los dos bytes del company id). Devuelve false si no hay ninguno o está malformado.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> appleManufacturerData, out AirPodsStatus? status)
    {
        status = null;
        // Apple encadena varios mensajes Continuity como TLV (tipo, longitud, valor) en el mismo anuncio.
        var remaining = appleManufacturerData;
        while (remaining.Length >= 2)
        {
            byte type = remaining[0];
            int length = remaining[1];
            if (remaining.Length < 2 + length)
            {
                return false;
            }

            if (type == ProximityPairingType)
            {
                return TryParseMessage(remaining[..(2 + length)], out status);
            }

            remaining = remaining[(2 + length)..];
        }

        return false;
    }

    private static bool TryParseMessage(ReadOnlySpan<byte> message, out AirPodsStatus? status)
    {
        status = null;
        if (message.Length < ClearTextLength)
        {
            return false;
        }

        ushort productId = BinaryPrimitives.ReadUInt16LittleEndian(message[ProductIdOffset..]);
        byte flags = message[StatusOffset];
        byte pods = message[PodBatteriesOffset];
        byte chargingAndCase = message[ChargingAndCaseOffset];

        // El auricular que emite («primario») va en el nibble bajo; el otro, en el alto.
        if (!TryReadBattery(pods & 0x0F, out int? primaryPercent)
            || !TryReadBattery(pods >> 4, out int? secondaryPercent)
            || !TryReadBattery(chargingAndCase & 0x0F, out int? casePercent))
        {
            return false;
        }

        int chargingFlags = chargingAndCase >> 4;
        bool primaryIsLeft = (flags & PrimaryIsLeftFlag) != 0;
        bool bothInCase = (flags & BothPodsInCaseFlag) != 0;
        bool primaryInCase = bothInCase || (flags & PrimaryInCaseFlag) != 0;
        bool secondaryInCase = bothInCase || (!primaryInCase && (flags & OnePodInCaseFlag) != 0);

        var primary = new PodStatus(
            new BatteryReading(primaryPercent, primaryPercent.HasValue && (chargingFlags & 0x1) != 0),
            IsInEar: !primaryInCase && (flags & PrimaryInEarFlag) != 0,
            IsInCase: primaryInCase);
        var secondary = new PodStatus(
            new BatteryReading(secondaryPercent, secondaryPercent.HasValue && (chargingFlags & 0x2) != 0),
            IsInEar: !secondaryInCase && (flags & SecondaryInEarFlag) != 0,
            IsInCase: secondaryInCase);
        var caseBattery = new BatteryReading(casePercent, casePercent.HasValue && (chargingFlags & 0x4) != 0);

        // El estado de la tapa solo tiene sentido si hay algún auricular dentro del estuche.
        bool lidOpen = (primaryInCase || secondaryInCase) && (message[LidOffset] & LidClosedFlag) == 0;

        status = new AirPodsStatus(
            AirPodsModels.FromProductId(productId),
            productId,
            Left: primaryIsLeft ? primary : secondary,
            Right: primaryIsLeft ? secondary : primary,
            caseBattery,
            lidOpen);
        return true;
    }

    private static bool TryReadBattery(int nibble, out int? percent)
    {
        percent = null;
        if (nibble == UnavailableNibble)
        {
            return true;
        }

        if (nibble > MaxBatteryNibble)
        {
            return false;
        }

        percent = nibble * 10;
        return true;
    }
}
