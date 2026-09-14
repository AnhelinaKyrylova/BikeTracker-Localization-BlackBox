namespace BikeTracker.Core.Interfaces
{
    public enum ComputeTier
    {
        Low,
        Medium,
        High
    }

    public interface IDeviceCapabilities
    {
        ComputeTier Tier { get; }
        bool HasFpu { get; }
        int AvailableRamKb { get; }
    }
}