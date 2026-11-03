namespace PodDeck.Core.Devices;

/// <summary>Estado de un auricular: su batería y dónde está.</summary>
public readonly record struct PodStatus(BatteryReading Battery, bool IsInEar, bool IsInCase);
