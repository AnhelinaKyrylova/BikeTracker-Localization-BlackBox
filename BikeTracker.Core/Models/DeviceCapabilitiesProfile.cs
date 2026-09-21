using BikeTracker.Core.Interfaces;

namespace BikeTracker.Core.Models
{

    /// Vytvaratelna implementacia IDeviceCapabilities.
    /// Platformovy kod (MAUI, testy) len naplni tieto hodnoty podľa toho,
    /// na com appka bude realne bezat.

    public class DeviceCapabilitiesProfile : IDeviceCapabilities
    {
        public ComputeTier Tier { get; set; }
        public bool HasFpu { get; set; }
        public int AvailableRamKb { get; set; }

        public static DeviceCapabilitiesProfile HighTierPhone() => new DeviceCapabilitiesProfile
        {
            Tier = ComputeTier.High,
            HasFpu = true,
            AvailableRamKb = 2_000_000 // radovo GB, hruby odhad
        };

        public static DeviceCapabilitiesProfile LowTierEmbedded() => new DeviceCapabilitiesProfile
        {
            Tier = ComputeTier.Low,
            HasFpu = false,
            AvailableRamKb = 128 // radovo kB, typicke pre maly MCU
        };
    }
}