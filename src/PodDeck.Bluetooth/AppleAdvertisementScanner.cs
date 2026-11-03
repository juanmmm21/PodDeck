using PodDeck.Core.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace PodDeck.Bluetooth;

/// <summary>
/// Escucha anuncios BLE con datos de fabricante de Apple usando el watcher de WinRT. No empareja
/// ni conecta: solo lee lo que los dispositivos emiten a cualquiera que esté cerca.
/// </summary>
public sealed class AppleAdvertisementScanner : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private bool _disposed;

    public AppleAdvertisementScanner()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            // Los AirPods mandan el mensaje de proximidad en el anuncio, no en la respuesta al
            // escaneo, así que el modo pasivo basta y no despierta a nadie con peticiones.
            ScanningMode = BluetoothLEScanningMode.Passive,
        };
        _watcher.AdvertisementFilter.Advertisement.ManufacturerData.Add(
            new BluetoothLEManufacturerData { CompanyId = ProximityPairingParser.AppleCompanyId });
        _watcher.Received += OnReceived;
        _watcher.Stopped += OnStopped;
    }

    /// <summary>Se dispara desde un hilo del pool de WinRT, no desde el hilo de la interfaz.</summary>
    public event EventHandler<AppleAdvertisement>? AdvertisementReceived;

    /// <summary>
    /// El watcher se ha parado. <see cref="BluetoothError.Success"/> indica una parada pedida con
    /// <see cref="Stop"/>; cualquier otro valor (radio apagada, sin adaptador...) es un fallo.
    /// </summary>
    public event EventHandler<BluetoothError>? Stopped;

    public bool IsRunning => _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _watcher.Start();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_watcher.Status is BluetoothLEAdvertisementWatcherStatus.Started
            or BluetoothLEAdvertisementWatcherStatus.Created)
        {
            _watcher.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watcher.Received -= OnReceived;
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            _watcher.Stop();
        }

        _watcher.Stopped -= OnStopped;
        _disposed = true;
    }

    private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (BluetoothLEManufacturerData manufacturerData in args.Advertisement.ManufacturerData)
        {
            if (manufacturerData.CompanyId != ProximityPairingParser.AppleCompanyId)
            {
                continue;
            }

            byte[] data = new byte[manufacturerData.Data.Length];
            using (DataReader reader = DataReader.FromBuffer(manufacturerData.Data))
            {
                reader.ReadBytes(data);
            }

            AdvertisementReceived?.Invoke(this, new AppleAdvertisement(
                args.BluetoothAddress, args.RawSignalStrengthInDBm, args.Timestamp, data));
        }
    }

    private void OnStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args) =>
        Stopped?.Invoke(this, args.Error);
}
