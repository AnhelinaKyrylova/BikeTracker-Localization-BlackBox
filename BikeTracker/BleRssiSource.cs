using System.Diagnostics;
using BikeTracker.Core.Models;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace BikeTracker;

/// Zivy zdroj RSSI vzoriek z cieloveho BLE zariadenia (Calvin_E).
/// Platformova vrstva: Plugin.BLE -> RssiSample pre black box.
/// Zamkne sa na prve zariadenie, ktore splna kriteria ciela.
public sealed class BleRssiSource
{
    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;
    private readonly Stopwatch _clock = new();
    private readonly object _lock = new();

    private Task? _scanTask;
    private Guid? _lockedDeviceId;

    /// Vyvola sa pre kazdy paket z ciela (na vlakne BLE, handler musi byt rychly).
    public event Action<RssiSample>? SampleReceived;

    public bool IsRunning { get; private set; }

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        lock (_lock) { _lockedDeviceId = null; }
        _clock.Restart();

        _adapter.ScanMode = ScanMode.LowLatency;
        _adapter.ScanTimeout = 3_600_000; // 1 hodina
        _adapter.DeviceAdvertised += OnDeviceAdvertised;

        _scanTask = _adapter.StartScanningForDevicesAsync();
        IsRunning = true;
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        IsRunning = false;

        _adapter.DeviceAdvertised -= OnDeviceAdvertised;

        try { await _adapter.StopScanningForDevicesAsync(); } catch { }

        if (_scanTask != null)
        {
            try { await _scanTask; } catch { }
            _scanTask = null;
        }
    }

    private void OnDeviceAdvertised(object? sender, DeviceEventArgs e)
    {
        IDevice device = e.Device;

        if (!BleRssiLogger.IsTarget(device, BleRssiLogger.GetAdvertisedName(device))) return;

        lock (_lock)
        {
            if (_lockedDeviceId == null) _lockedDeviceId = device.Id;
            else if (device.Id != _lockedDeviceId.Value) return;
        }

        SampleReceived?.Invoke(new RssiSample
        {
            DeviceId = device.Id.ToString(),
            Rssi = device.Rssi,
            TimestampMs = _clock.ElapsedMilliseconds
        });
    }
}