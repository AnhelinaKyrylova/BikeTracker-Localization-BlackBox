using Microsoft.Maui.Devices.Sensors;

namespace BikeTracker;

/// Senzory telefonu pre logger: kompas (smer telefonu) a detekcia pohybu
/// (akcelerometer + gyroskop). Pohyb alebo otacanie telefonu meni RSSI,
/// preto sa kazdy riadok v CSV oznaci priznakom phone_moving.
/// Prahy nizsie su orientacne - ak by hlasil pohyb aj na stole, zvys ich.
public sealed class PhoneSensors
{
    private const int AccelWindow = 15;                  // asi 1 s pri SensorSpeed.UI
    private const double AccelSdThresholdG = 0.02;       // smerodajna odchylka |a| v g
    private const double GyroThresholdRadPerSec = 0.15;  // asi 9 stupnov za sekundu

    private readonly Queue<double> _accel = new();
    private readonly object _lock = new();
    private double _accelSd;
    private double _gyroMag;

    /// Smer telefonu v stupnoch (0 = magneticky sever), NaN ak kompas nie je dostupny.
    public double CompassDeg { get; private set; } = double.NaN;

    /// True, ak sa telefon pohybuje alebo otaca.
    public bool IsMoving { get; private set; }

    /// Vyvolane pri kazdej zmene (kompas, pohyb).
    public event Action<double, bool>? StateChanged;

    public void Start()
    {
        lock (_lock)
        {
            _accel.Clear();
            _accelSd = 0;
            _gyroMag = 0;
            IsMoving = false;
            CompassDeg = double.NaN;
        }

        try
        {
            if (Accelerometer.Default.IsSupported && !Accelerometer.Default.IsMonitoring)
            {
                Accelerometer.Default.ReadingChanged += OnAccelerometer;
                Accelerometer.Default.Start(SensorSpeed.UI);
            }
        }
        catch { /* senzor nie je dostupny */ }

        try
        {
            if (Gyroscope.Default.IsSupported && !Gyroscope.Default.IsMonitoring)
            {
                Gyroscope.Default.ReadingChanged += OnGyroscope;
                Gyroscope.Default.Start(SensorSpeed.UI);
            }
        }
        catch { }

        try
        {
            if (Compass.Default.IsSupported && !Compass.Default.IsMonitoring)
            {
                Compass.Default.ReadingChanged += OnCompass;
                Compass.Default.Start(SensorSpeed.UI);
            }
        }
        catch { }
    }

    public void Stop()
    {
        try
        {
            Accelerometer.Default.ReadingChanged -= OnAccelerometer;
            if (Accelerometer.Default.IsMonitoring) Accelerometer.Default.Stop();
        }
        catch { }

        try
        {
            Gyroscope.Default.ReadingChanged -= OnGyroscope;
            if (Gyroscope.Default.IsMonitoring) Gyroscope.Default.Stop();
        }
        catch { }

        try
        {
            Compass.Default.ReadingChanged -= OnCompass;
            if (Compass.Default.IsMonitoring) Compass.Default.Stop();
        }
        catch { }
    }

    private void OnAccelerometer(object? sender, AccelerometerChangedEventArgs e)
    {
        double magnitude = e.Reading.Acceleration.Length(); // v g, v pokoji asi 1.0

        lock (_lock)
        {
            _accel.Enqueue(magnitude);
            while (_accel.Count > AccelWindow) _accel.Dequeue();

            if (_accel.Count >= 5)
            {
                double mean = _accel.Average();
                double variance = _accel.Sum(a => (a - mean) * (a - mean)) / _accel.Count;
                _accelSd = Math.Sqrt(variance);
            }

            IsMoving = _accelSd > AccelSdThresholdG || _gyroMag > GyroThresholdRadPerSec;
        }

        StateChanged?.Invoke(CompassDeg, IsMoving);
    }

    private void OnGyroscope(object? sender, GyroscopeChangedEventArgs e)
    {
        lock (_lock)
        {
            _gyroMag = e.Reading.AngularVelocity.Length(); // rad/s
            IsMoving = _accelSd > AccelSdThresholdG || _gyroMag > GyroThresholdRadPerSec;
        }

        StateChanged?.Invoke(CompassDeg, IsMoving);
    }

    private void OnCompass(object? sender, CompassChangedEventArgs e)
    {
        CompassDeg = e.Reading.HeadingMagneticNorth;
        StateChanged?.Invoke(CompassDeg, IsMoving);
    }
}