using BikeTracker.Core.Interfaces;
using BikeTracker.Core.Models;

namespace BikeTracker.Core
{
    /// <summary>
    /// Jediny verejny vstupny bod black boxu. Platformovy kod (MAUI app,
    /// buduci embedded firmware) pozna len tuto triedu - nic o vnutornej
    /// volbe alebo implementacii filtrov.
    /// </summary>
    public class LocalizationBlackBox
    {
        private readonly ILocalizationFilter _filter;

        public LocalizationBlackBox(IDeviceCapabilities capabilities, IFilterSelector selector = null)
        {
            selector = selector ?? new BikeTracker.Core.Filters.DefaultFilterSelector();
            _filter = selector.SelectFilter(capabilities);
        }

        public PositionEstimate Process(RssiSample sample) => _filter.Update(sample);

        public void Reset() => _filter.Reset();
    }
}