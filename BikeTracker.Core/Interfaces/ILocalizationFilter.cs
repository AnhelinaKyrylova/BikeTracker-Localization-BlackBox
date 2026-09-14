using BikeTracker.Core.Models;

namespace BikeTracker.Core.Interfaces
{
    public interface ILocalizationFilter
    {
        void Reset();
        PositionEstimate Update(RssiSample sample);
    }
}