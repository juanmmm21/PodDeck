namespace PodDeck.Bluetooth;

/// <summary>Datos de fabricante de Apple de un anuncio BLE, sin los dos bytes del company id.</summary>
public sealed record AppleAdvertisement(
    ulong BluetoothAddress,
    short SignalStrengthDbm,
    DateTimeOffset Timestamp,
    ReadOnlyMemory<byte> Data);
