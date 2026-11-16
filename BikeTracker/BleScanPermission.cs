namespace BikeTracker;

/// Povolenia potrebne na BLE skenovanie.
/// Android 12+ (API 31): BLUETOOTH_SCAN + BLUETOOTH_CONNECT,
/// starsie verzie: poloha (ACCESS_FINE_LOCATION).
public class BleScanPermission : Permissions.BasePlatformPermission
{
#if ANDROID
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
    {
        get
        {
            var list = new List<(string androidPermission, bool isRuntime)>
            {
                (global::Android.Manifest.Permission.AccessFineLocation, true)
            };

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                list.Add((global::Android.Manifest.Permission.BluetoothScan, true));
                list.Add((global::Android.Manifest.Permission.BluetoothConnect, true));
            }

            return list.ToArray();
        }
    }
#endif
}