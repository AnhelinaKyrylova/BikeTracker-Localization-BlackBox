using BikeTracker.Core.Interfaces;
using BikeTracker.Core.Models;
using BikeTracker.Core.Filters;

namespace BikeTracker.Core
{

    /// Vstupny bod black boxu. Platformovy kod pozna len tuto triedu, 
    /// nic o vnutornej volbe alebo implementacii filtrov.

    public class LocalizationBlackBox
    {
        private readonly ILocalizationFilter _filter;
        private readonly RssiHeadingEstimator _headingEstimator;

        public LocalizationBlackBox(IDeviceCapabilities capabilities, IFilterSelector selector = null)
        {
            selector = selector ?? new BikeTracker.Core.Filters.DefaultFilterSelector();
            _filter = selector.SelectFilter(capabilities);
            _headingEstimator = new RssiHeadingEstimator(); 
        }
        /// Spracuje jednu RSSI vzorku a vrati aktualny odhad vzdialenosti.
        public PositionEstimate Process(RssiSample sample) => _filter.Update(sample);

        public void Reset() => _filter.Reset();

        /// Zacne novy sweep na urcenie smeru (zahodi predchadzajuce vzorky smeru).
        public void StartHeadingSweep() => _headingEstimator.Reset();

        /// Prida jednu vzorku pocas sweepu (kompasovy smer telefonu + RSSI v danom okamihu).
        public void AddHeadingSample(double deviceCompassHeadingDeg, double rssi) =>
            _headingEstimator.AddSample(deviceCompassHeadingDeg, rssi);

         /// Aktualny odhad smeru k tagu na zaklade doteraz zozbieranych vzoriek sweepu.

        public HeadingEstimate GetHeadingEstimate() => _headingEstimator.GetEstimate();

    }
}