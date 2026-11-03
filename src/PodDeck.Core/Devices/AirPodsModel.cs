namespace PodDeck.Core.Devices;

/// <summary>Modelos de AirPods que PodDeck reconoce por su identificador de producto.</summary>
public enum AirPodsModel
{
    Unknown,
    AirPods1,
    AirPods2,
    AirPods3,
    AirPods4,
    AirPods4Anc,
    AirPodsPro,
    AirPodsPro2,
    AirPodsPro2UsbC,
    AirPodsPro3,
    AirPodsMax,
    AirPodsMaxUsbC,
}

/// <summary>Traduce el identificador de producto del anuncio a un modelo y a su nombre comercial.</summary>
public static class AirPodsModels
{
    // Identificadores tal y como llegan en el anuncio (bytes 3-4 en little-endian). Apple no los
    // publica: salen de la ingeniería inversa de AirPodsDesktop, OpenPods y LibrePods.
    private static readonly Dictionary<ushort, AirPodsModel> ModelsById = new()
    {
        [0x2002] = AirPodsModel.AirPods1,
        [0x200F] = AirPodsModel.AirPods2,
        [0x2013] = AirPodsModel.AirPods3,
        [0x2019] = AirPodsModel.AirPods4,
        [0x201B] = AirPodsModel.AirPods4Anc,
        [0x200E] = AirPodsModel.AirPodsPro,
        [0x2014] = AirPodsModel.AirPodsPro2,
        [0x2024] = AirPodsModel.AirPodsPro2UsbC,
        [0x2027] = AirPodsModel.AirPodsPro3,
        [0x200A] = AirPodsModel.AirPodsMax,
        [0x201F] = AirPodsModel.AirPodsMaxUsbC,
    };

    public static AirPodsModel FromProductId(ushort productId) =>
        ModelsById.GetValueOrDefault(productId, AirPodsModel.Unknown);

    /// <summary>Los AirPods Max son unos cascos: una sola batería y sin estuche que informe.</summary>
    public static bool IsHeadphones(this AirPodsModel model) =>
        model is AirPodsModel.AirPodsMax or AirPodsModel.AirPodsMaxUsbC;

    public static string DisplayName(this AirPodsModel model) => model switch
    {
        AirPodsModel.AirPods1 => "AirPods (1st generation)",
        AirPodsModel.AirPods2 => "AirPods (2nd generation)",
        AirPodsModel.AirPods3 => "AirPods (3rd generation)",
        AirPodsModel.AirPods4 => "AirPods 4",
        AirPodsModel.AirPods4Anc => "AirPods 4 (ANC)",
        AirPodsModel.AirPodsPro => "AirPods Pro",
        AirPodsModel.AirPodsPro2 => "AirPods Pro (2nd generation)",
        AirPodsModel.AirPodsPro2UsbC => "AirPods Pro (2nd generation, USB-C)",
        AirPodsModel.AirPodsPro3 => "AirPods Pro 3",
        AirPodsModel.AirPodsMax => "AirPods Max",
        AirPodsModel.AirPodsMaxUsbC => "AirPods Max (USB-C)",
        AirPodsModel.Unknown => "AirPods",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unhandled AirPods model."),
    };
}
