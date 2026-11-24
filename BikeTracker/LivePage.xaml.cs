using System.Diagnostics;
using System.Globalization;
using BikeTracker.Core;
using BikeTracker.Core.Filters;
using BikeTracker.Core.Models;
using Plugin.BLE;

namespace BikeTracker;

public partial class LivePage : ContentPage
{
    // TODO: ZATIAL ODHAD, treba prekalibrovat na Calvin_E.
    // Merania z izby boli nekonzistentne (1 m: -75 az -86 dBm), preto len stred.
    private const double TxPowerAt1m = -75.0;
    private const double PathLossExponent = 2.5;

    private const int SweepSeconds = 10;

    private readonly BleRssiSource _source = new();
    private readonly PhoneSensors _sensors = new();
    private readonly object _lock = new();

    private LocalizationBlackBox _box = CreateBlackBox();
    private PositionEstimate? _latest;
    private HeadingEstimate? _heading;
    private int _lastRssi;
    private int _sampleCount;
    private bool _sweeping;
    private bool _running;
    private IDispatcherTimer? _uiTimer;

    // meranie vypoctovej narocnosti black boxu (Process) priamo na telefone
    private long _processTicks;
    private int _processCalls;

    public LivePage()
    {
        InitializeComponent();
    }

    private static LocalizationBlackBox CreateBlackBox()
    {
        var model = new RssiPathLossModel { TxPower = TxPowerAt1m, PathLossExponent = PathLossExponent };
        return new LocalizationBlackBox(
            DeviceCapabilitiesProfile.HighTierPhone(),
            new DefaultFilterSelector(model));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_uiTimer == null)
        {
            _uiTimer = Dispatcher.CreateTimer();
            _uiTimer.Interval = TimeSpan.FromMilliseconds(500);
            _uiTimer.Tick += (s, e) => UpdateUi();
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_running) await StopAsync();
    }

    // vola sa na vlakne BLE: vsetky volania black boxu su pod zamkom
    private void OnSampleReceived(RssiSample sample)
    {
        lock (_lock)
        {
            _lastRssi = sample.Rssi;
            _sampleCount++;

            long t0 = Stopwatch.GetTimestamp();
            _latest = _box.Process(sample);
            _processTicks += Stopwatch.GetTimestamp() - t0;
            _processCalls++;

            double compass = _sensors.CompassDeg;
            if (_sweeping && !double.IsNaN(compass))
                _box.AddHeadingSample(compass, sample.Rssi);
        }
    }

    private async void OnStartStopClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_running)
            {
                await StopAsync();
                return;
            }

            var status = await Permissions.RequestAsync<BleScanPermission>();
            if (status != PermissionStatus.Granted)
            {
                StatusLabel.Text = "Chýbajú povolenia (Bluetooth / poloha).";
                return;
            }

            if (!CrossBluetoothLE.Current.IsOn)
            {
                StatusLabel.Text = "Zapni Bluetooth.";
                return;
            }

            lock (_lock)
            {
                _box = CreateBlackBox();
                _latest = null;
                _heading = null;
                _sampleCount = 0;
                _sweeping = false;
                _processTicks = 0;
                _processCalls = 0;
            }

            ArrowLabel.Rotation = 0;
            ArrowLabel.Opacity = 0.3;
            HeadingLabel.Text = "Smer: zatiaľ nezmeraný";

            _sensors.Start();
            _source.SampleReceived += OnSampleReceived;
            await _source.StartAsync();

            _running = true;
            StartStopBtn.Text = "Stop";
            SweepBtn.IsEnabled = true;
            _uiTimer?.Start();
            StatusLabel.Text = "Skenujem, čakám na pakety...";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Chyba: {ex.Message}";
        }
    }

    private async Task StopAsync()
    {
        _running = false;
        _uiTimer?.Stop();

        lock (_lock) { _sweeping = false; }

        _source.SampleReceived -= OnSampleReceived;
        await _source.StopAsync();
        _sensors.Stop();

        StartStopBtn.Text = "Štart";
        SweepBtn.IsEnabled = false;
        StatusLabel.Text = "Zastavené.";
    }

    private async void OnSweepClicked(object? sender, EventArgs e)
    {
        if (!_running || _sweeping) return;

        lock (_lock)
        {
            _box.StartHeadingSweep();
            _heading = null;
            _sweeping = true;
        }

        SweepBtn.IsEnabled = false;

        // telefon drz vodorovne a pomaly ho otoc o 360 stupnov
        for (int s = SweepSeconds; s > 0; s--)
        {
            StatusLabel.Text = $"Sweep: pomaly otáčaj telefón dokola... {s} s";
            await Task.Delay(1000);
        }

        HeadingEstimate estimate;
        lock (_lock)
        {
            _sweeping = false;
            estimate = _box.GetHeadingEstimate();
            _heading = estimate;
        }

        SweepBtn.IsEnabled = _running;

        HeadingLabel.Text =
            $"Smer k tagu: {estimate.HeadingDeg.ToString("F0", CultureInfo.InvariantCulture)}° | " +
            $"istota {estimate.Confidence:P0} | pokrytie {estimate.FilledBins}/{estimate.TotalBins}";

        // prah 0.3 je zatial len odhad
        StatusLabel.Text = estimate.Confidence < 0.3
            ? "Smer je neistý (slabé maximum alebo neúplné otočenie). Skús znova."
            : "Smer zmeraný.";
    }

    private void UpdateUi()
    {
        PositionEstimate? latest;
        HeadingEstimate? heading;
        int rssi, count, calls;
        long ticks;
        string filterName;

        lock (_lock)
        {
            latest = _latest;
            heading = _heading;
            rssi = _lastRssi;
            count = _sampleCount;
            ticks = _processTicks;
            calls = _processCalls;
            filterName = _box.FilterName;
        }

        if (latest == null)
        {
            DistanceLabel.Text = "- m";
        }
        else
        {
            DistanceLabel.Text = latest.DistanceM.ToString("F1", CultureInfo.InvariantCulture) + " m";
            DistanceInfoLabel.Text = $"RSSI {rssi} dBm | istota {latest.Confidence:P0} | pakety {count}";
        }

        if (calls > 0)
        {
            double microseconds = ticks * 1_000_000.0 / Stopwatch.Frequency / calls;
            FilterLabel.Text = $"Filter: {filterName} | priemerný čas Process(): {microseconds:F0} µs";
        }

        // sipka ukazuje smer k tagu relativne k tomu, kam teraz smeruje telefon
        double compass = _sensors.CompassDeg;
        if (heading != null && heading.Confidence > 0 && !double.IsNaN(compass))
        {
            ArrowLabel.Rotation = ((heading.HeadingDeg - compass) % 360 + 360) % 360;
            ArrowLabel.Opacity = 0.3 + 0.7 * heading.Confidence;
        }
    }
}