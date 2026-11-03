using PodDeck.Core.Pairing;
using Windows.Devices.Enumeration;

namespace PodDeck.Bluetooth;

/// <summary>
/// Vigila qué AirPods (o cualquier audio de Apple) están emparejados con el PC y avisa de sus
/// product ids, para que el tracker ignore los AirPods de otras personas.
/// </summary>
/// <remarks>
/// Los AirPods se emparejan por Bluetooth clásico, así que no hay clave LE con la que reconocer sus
/// anuncios. Lo que sí deja Windows es un nodo por cada servicio emparejado con el VID/PID del perfil
/// Device ID en el identificador de instancia, y ese PID coincide con el del anuncio.
/// </remarks>
public sealed class PairedAirPodsWatcher : IDisposable
{
    private const string InstanceIdProperty = "System.Devices.DeviceInstanceId";

    // Filtro AQS por subcadena: solo nodos de servicios Bluetooth publicados por Apple (VID 0x004C).
    private const string AppleBluetoothServicesQuery = "System.Devices.DeviceInstanceId:~~\"_VID&0001004C_PID&\"";

    private readonly DeviceWatcher _watcher;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ushort> _productsByDevice = new(StringComparer.OrdinalIgnoreCase);
    private bool _enumerated;
    private bool _disposed;

    public PairedAirPodsWatcher()
    {
        _watcher = DeviceInformation.CreateWatcher(
            AppleBluetoothServicesQuery, [InstanceIdProperty], DeviceInformationKind.Device);
        _watcher.Added += OnAdded;
        _watcher.Removed += OnRemoved;
        _watcher.Updated += OnUpdated;
        _watcher.EnumerationCompleted += OnEnumerationCompleted;
    }

    /// <summary>
    /// Conjunto completo de product ids emparejados. Se dispara al terminar la primera enumeración y
    /// cada vez que cambia después, desde un hilo del pool de WinRT.
    /// </summary>
    public event EventHandler<IReadOnlySet<ushort>>? PairedProductsChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _watcher.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watcher.Added -= OnAdded;
        _watcher.Removed -= OnRemoved;
        _watcher.Updated -= OnUpdated;
        _watcher.EnumerationCompleted -= OnEnumerationCompleted;
        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }

        _disposed = true;
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        string instanceId = device.Properties.TryGetValue(InstanceIdProperty, out object? value) && value is string id
            ? id
            : device.Id;
        if (!PairedDeviceId.TryParseAppleProductId(instanceId, out ushort productId))
        {
            return;
        }

        lock (_gate)
        {
            _productsByDevice[device.Id] = productId;
        }

        PublishIfEnumerated();
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        bool removed;
        lock (_gate)
        {
            removed = _productsByDevice.Remove(update.Id);
        }

        if (removed)
        {
            PublishIfEnumerated();
        }
    }

    // Sin suscriptor a Updated, WinRT no entrega Added/Removed después de la enumeración inicial.
    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        lock (_gate)
        {
            _enumerated = true;
        }

        PublishIfEnumerated();
    }

    private void PublishIfEnumerated()
    {
        HashSet<ushort> products;
        lock (_gate)
        {
            if (!_enumerated)
            {
                return;
            }

            products = [.. _productsByDevice.Values];
        }

        PairedProductsChanged?.Invoke(this, products);
    }
}
