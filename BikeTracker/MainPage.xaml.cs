using Plugin.BLE;
using System.Globalization;

namespace BikeTracker;

public partial class MainPage : ContentPage
{
    private readonly BleRssiLogger _logger = new();
    private readonly PhoneSensors _sensors = new();
    private IDispatcherTimer? _uiTimer;
    private int _previousCount;

    public MainPage()
    {
        InitializeComponent();

        // stav senzorov telefonu sa posiela do loggeru a zapisuje do kazdeho riadku CSV
        _sensors.StateChanged += (compass, moving) => _logger.SetPhoneState(compass, moving);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_uiTimer == null)
        {
            _uiTimer = Dispatcher.CreateTimer();
            _uiTimer.Interval = TimeSpan.FromMilliseconds(500);
            _uiTimer.Tick += (s, e) => UpdateStatus();
        }
    }

    // Vzdialenost a scenar sa daju menit aj pocas zaznamu.
    // Kazda zmena zacne novy segment (v CSV stlpec segment_id).
    private void OnLabelChanged(object? sender, EventArgs e) => ApplyLabels();
    private void OnTagChanged(object? sender, CheckedChangedEventArgs e) => ApplyLabels();

    private string BuildScenario()
    {
        var tags = new List<string>();
        if (TagLos.IsChecked) tags.Add("los");
        if (TagClovek.IsChecked) tags.Add("clovek");
        if (TagStena.IsChecked) tags.Add("stena");
        if (TagKov.IsChecked) tags.Add("kov");
        if (TagStol.IsChecked) tags.Add("stol");
        if (TagVonku.IsChecked) tags.Add("vonku");
        return string.Join("+", tags);
    }

    private void ApplyLabels()
    {
        string distance = (DistanceEntry.Text ?? "").Trim().Replace(',', '.').Replace(';', '_');
        string scenario = BuildScenario();

        bool changed = distance != _logger.DistanceM || scenario != _logger.Scenario;

        _logger.DistanceM = distance;
        _logger.Scenario = scenario;

        if (changed && _logger.IsRunning)
            _logger.StartNewSegment();
    }

    private async void OnStartStopClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_logger.IsRunning)
            {
                await StopLoggingAsync();
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

            ApplyLabels();

            if (!double.TryParse(_logger.DistanceM, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                StatusLabel.Text = "Zadaj vzdialenosť číslom (napr. 1.5).";
                return;
            }

            if (string.IsNullOrEmpty(_logger.Scenario))
            {
                StatusLabel.Text = "Vyber aspoň jeden scenár (napr. LOS).";
                return;
            }

            _sensors.Start();
            await _logger.StartAsync();

            StartStopBtn.Text = "Stop záznamu";
            PauseBtn.IsEnabled = true;
            PauseBtn.Text = "Pauza (presúvam zariadenie)";
            ShareBtn.IsEnabled = false;
            _previousCount = 0;
            _uiTimer?.Start();
            StatusLabel.Text = "Záznam beží, čakám na pakety...";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Chyba: {ex.Message}";
        }
    }

    private void OnPauseClicked(object? sender, EventArgs e)
    {
        if (!_logger.IsRunning) return;

        _logger.Paused = !_logger.Paused;

        if (_logger.Paused)
        {
            PauseBtn.Text = "Pokračovať v zázname";
        }
        else
        {
            // po pauze mohlo zariadenie zmenit miesto: zober novu vzdialenost/scenar a zacni novy segment
            ApplyLabels();
            _logger.StartNewSegment();
            PauseBtn.Text = "Pauza (presúvam zariadenie)";
        }
    }

    private async Task StopLoggingAsync()
    {
        _uiTimer?.Stop();
        _sensors.Stop();
        await _logger.StopAsync();

        StartStopBtn.Text = "Štart záznamu";
        PauseBtn.IsEnabled = false;
        PauseBtn.Text = "Pauza (presúvam zariadenie)";
        ShareBtn.IsEnabled = _logger.FilePath != null;
        StatusLabel.Text =
            $"Zastavené. Zapísaných {_logger.SampleCount} paketov v {_logger.SegmentId} segmentoch " +
            $"(ignorovaných: {_logger.IgnoredTargetCount}).\n{_logger.FilePath}";
    }

    private void UpdateStatus()
    {
        int count = _logger.SampleCount;
        double rate = (count - _previousCount) / 0.5;
        _previousCount = count;

        string compass = double.IsNaN(_sensors.CompassDeg)
            ? "-"
            : _sensors.CompassDeg.ToString("F0", CultureInfo.InvariantCulture) + "°";

        string head = _logger.Paused
            ? "PAUZA - nezapisuje sa"
            : $"Záznam beží | cieľ: {count} ({rate:F1} pak/s) | RSSI: {_logger.LastRssi} dBm";

        StatusLabel.Text =
            head + "\n" +
            $"Segment: {_logger.SegmentId} | Zariadenie: {_logger.LockedDeviceShort}\n" +
            $"Telefón: {(_sensors.IsMoving ? "HÝBE SA" : "v pokoji")} | kompas: {compass}\n" +
            $"Celkom paketov: {_logger.TotalAdvertCount} | Ignorované: {_logger.IgnoredTargetCount} | Posledný názov: {_logger.LastSeenName}";
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_logger.FilePath == null) return;

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "BLE log",
                File = new ShareFile(_logger.FilePath)
            });
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Chyba pri zdieľaní: {ex.Message}";
        }
    }
}