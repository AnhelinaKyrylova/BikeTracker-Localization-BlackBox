using Plugin.BLE;

namespace BikeTracker;

public partial class MainPage : ContentPage
{
    private readonly BleRssiLogger _logger = new();
    private IDispatcherTimer? _uiTimer;
    private int _previousCount;

    public MainPage()
    {
        InitializeComponent();
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

    // Vzdialenost a scenar sa daju menit aj pocas zaznamu - kazdy riadok v CSV nesie aktualnu hodnotu.
    private void OnLabelChanged(object? sender, TextChangedEventArgs e) => ApplyEntries();

    private void ApplyEntries()
    {
        _logger.DistanceM = (DistanceEntry.Text ?? "").Trim().Replace(',', '.').Replace(';', '_');
        _logger.Scenario = (ScenarioEntry.Text ?? "").Trim().Replace(';', ',');
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

            ApplyEntries();
            await _logger.StartAsync();

            StartStopBtn.Text = "Stop záznamu";
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

    private async Task StopLoggingAsync()
    {
        _uiTimer?.Stop();
        await _logger.StopAsync();

        StartStopBtn.Text = "Štart záznamu";
        ShareBtn.IsEnabled = _logger.FilePath != null;
        StatusLabel.Text = $"Zastavené. Zapísaných {_logger.SampleCount} paketov.\n{_logger.FilePath}";
    }

		private void UpdateStatus()
	{
		int count = _logger.SampleCount;
		double rate = (count - _previousCount) / 0.5;
		_previousCount = count;

		StatusLabel.Text =
			$"Záznam beží | cieľ: {count} ({rate:F1} pak/s) | RSSI: {_logger.LastRssi} dBm\n" +
			$"Všetky pakety: {_logger.TotalAdvertCount} | posledný názov: {_logger.LastSeenName}";
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
