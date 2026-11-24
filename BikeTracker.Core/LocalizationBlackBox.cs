using BikeTracker.Core.Interfaces;
using BikeTracker.Core.Models;
using BikeTracker.Core.Filters;

namespace BikeTracker.Core
{
    /// Vstupny bod black boxu. Platformovy kod pozna len tuto triedu,
    /// nic o vnutornej volbe alebo implementacii filtrov.
    /// Trieda nie je vlaknovo bezpecna - volajuci kod ma volania synchronizovat.
    public class LocalizationBlackBox
    {
        private readonly ILocalizationFilter _filter;
        private readonly RssiHeadingEstimator _headingEstimator;

        // posledny odhad smeru zo sweepu (null = sweep este nebol)
        private HeadingEstimate _lastHeading;

        public LocalizationBlackBox(IDeviceCapabilities capabilities, IFilterSelector selector = null)
        {
            selector = selector ?? new DefaultFilterSelector();
            _filter = selector.SelectFilter(capabilities);
            _headingEstimator = new RssiHeadingEstimator();
        }

        /// Nazov zvoleneho filtra (napr. "ParticleFilter") - len diagnostika/zobrazenie,
        /// volajuci kod sa na nu nema opierat pri rozhodovani.
        public string FilterName => _filter.GetType().Name;

        /// Spracuje jednu RSSI vzorku. DistanceM pochadza z filtra,
        /// HeadingDeg (azimut k tagu od severu) zo sweepu, ak uz prebehol.
        public PositionEstimate Process(RssiSample sample)
        {
            PositionEstimate estimate = _filter.Update(sample);

            if (_lastHeading != null && _lastHeading.Confidence > 0)
                estimate.HeadingDeg = _lastHeading.HeadingDeg;

            return estimate;
        }

        public void Reset() => _filter.Reset();

        /// Zacne novy sweep na urcenie smeru (zahodi predchadzajuce vzorky smeru).
        public void StartHeadingSweep()
        {
            _headingEstimator.Reset();
            _lastHeading = null;
        }

        /// Prida jednu vzorku pocas sweepu (kompasovy smer telefonu + RSSI v danom okamihu).
        public void AddHeadingSample(double deviceCompassHeadingDeg, double rssi) =>
            _headingEstimator.AddSample(deviceCompassHeadingDeg, rssi);

        /// Odhad smeru k tagu zo zatial zozbieranych vzoriek sweepu.
        public HeadingEstimate GetHeadingEstimate()
        {
            _lastHeading = _headingEstimator.GetEstimate();
            return _lastHeading;
        }
    }
}