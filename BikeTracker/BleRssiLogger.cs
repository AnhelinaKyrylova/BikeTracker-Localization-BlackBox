using System.Diagnostics;
using System.Globalization;
using System.Text;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace BikeTracker;

/// Zaznamenava RSSI z jedneho konkretneho BLE zariadenia do CSV suboru.
///
/// Zariadenie vysiela len nazov "Calvin_E" a 128-bitove UUID sluzby
/// 00360010-8888-8888-8888-777777777777 (prvych 8 hex cifier = seriove cislo
/// z etikety). Ciel sa rozpozna podla UUID ALEBO podla nazvu z paketu.
///
/// Logger sa po prvom zhodnom pakete ZAMKNE na jedno zariadenie (podla device.Id).
/// Pakety z inych zariadeni, ktore by mali rovnaky nazov/UUID (napr. dalsia doska
/// "Calvin_E"), sa nezapisuju, len sa pocitaju v IgnoredTargetCount.
///
/// Format CSV: t_ms;unix_ms;rssi_dbm;distance_m;scenario;device_id
public sealed class BleRssiLogger
{
    public const string TargetName = "Calvin_E";

    // UUID bez pomlciek, v poradi ako sa zapisuje (big-endian)
    private const string TargetUuidHex = "00360010888888888888777777777777";

    private readonly IAdapter _adapter = CrossBluetoothLE.Current.Adapter;
    private readonly Stopwatch _clock = new();
    private readonly object _lock = new();

    private StreamWriter? _writer;
    private Task? _scanTask;

    // zariadenie, na ktore sa logger zamkol (null = zatial ziadne)
    private Guid? _lockedDeviceId;

    /// Aktualna vzdialenost od zariadenia v metroch (zapisuje sa do kazdeho riadku).
    public string DistanceM { get; set; } = "";

    /// Popis scenara (napr. LOS, stena, telo, kov).
    public string Scenario { get; set; } = "";

    public bool IsRunning { get; private set; }

    /// Pocet zapisanych paketov z ciela.
    public int SampleCount { get; private set; }

    /// Pocet VSETKYCH prijatych paketov (z akychkolvek zariadeni) - diagnostika.
    public int TotalAdvertCount { get; private set; }

    /// Pocet paketov, ktore splnaju kriteria ciela, ale prisli z ineho zariadenia
    /// nez to, na ktore sa logger zamkol (ignorovane) - diagnostika.
    public int IgnoredTargetCount { get; private set; }

    /// Nazov posledneho videneho zariadenia (aj necieloveho) - diagnostika.
    public string LastSeenName { get; private set; } = "";

    public int LastRssi { get; private set; }
    public string? FilePath { get; private set; }

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        FilePath = Path.Combine(FileSystem.AppDataDirectory, $"ble_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
        _writer.WriteLine("t_ms;unix_ms;rssi_dbm;distance_m;scenario;device_id");

        SampleCount = 0;
        TotalAdvertCount = 0;
        IgnoredTargetCount = 0;
        LastSeenName = "";
        LastRssi = 0;
        _lockedDeviceId = null;
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

        try { await _adapter.StopScanningForDevicesAsync(); } catch { /* skenovanie uz mohlo skoncit */ }

        if (_scanTask != null)
        {
            try { await _scanTask; } catch { }
            _scanTask = null;
        }

        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void OnDeviceAdvertised(object? sender, DeviceEventArgs e)
    {
        IDevice device = e.Device;
        string advertisedName = GetAdvertisedName(device);

        lock (_lock)
        {
            TotalAdvertCount++;
            if (!string.IsNullOrEmpty(advertisedName)) LastSeenName = advertisedName;
        }

        if (!IsTarget(device, advertisedName)) return;

        long tMs = _clock.ElapsedMilliseconds;
        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int rssi = device.Rssi;

        lock (_lock)
        {
            if (_writer == null) return;

            // zamkni sa na prve zariadenie, ktore splna kriteria ciela;
            // dalsie zariadenia s rovnakym nazvom/UUID ignoruj
            if (_lockedDeviceId == null)
            {
                _lockedDeviceId = device.Id;
            }
            else if (device.Id != _lockedDeviceId.Value)
            {
                IgnoredTargetCount++;
                return;
            }

            _writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0};{1};{2};{3};{4};{5}", tMs, unixMs, rssi, DistanceM, Scenario, device.Id));
            SampleCount++;
            LastRssi = rssi;
        }
    }

    /// Nazov z reklamneho paketu (Complete/Shortened Local Name), inak nazov zo systemu.
    private static string GetAdvertisedName(IDevice device)
    {
        foreach (var record in device.AdvertisementRecords)
        {
            if (record.Type == AdvertisementRecordType.CompleteLocalName ||
                record.Type == AdvertisementRecordType.ShortLocalName)
            {
                try { return Encoding.UTF8.GetString(record.Data); }
                catch { }
            }
        }

        return device.Name ?? "";
    }

    /// Ciel = 128-bitove UUID sluzby zhoduje s TargetUuidHex, ALEBO nazov je "Calvin_E".
    private static bool IsTarget(IDevice device, string advertisedName)
    {
        if (advertisedName == TargetName) return true;

        foreach (var record in device.AdvertisementRecords)
        {
            if (record.Type != AdvertisementRecordType.UuidsComplete128Bit &&
                record.Type != AdvertisementRecordType.UuidsIncomplete128Bit)
                continue;

            byte[] data = record.Data;

            // UUID v pakete su little-endian, po 16 bajtov
            for (int i = 0; i + 16 <= data.Length; i += 16)
            {
                byte[] slice = new byte[16];
                Array.Copy(data, i, slice, 0, 16);
                Array.Reverse(slice);
                if (Convert.ToHexString(slice) == TargetUuidHex) return true;
            }
        }

        return false;
    }
}