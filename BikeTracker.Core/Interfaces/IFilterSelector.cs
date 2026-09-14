namespace BikeTracker.Core.Interfaces
{
    public interface IFilterSelector
    {
        ILocalizationFilter SelectFilter(IDeviceCapabilities capabilities);
    }
}