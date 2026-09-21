using BikeTracker.Core.Interfaces;

namespace BikeTracker.Core.Filters
{

    /// Strategy: rozhoduje, ktory ILocalizationFilter sa nasadi,
    /// na zaklade schopnosti zariadenia.
    /// Skalovatelna voľba filtra.
  
    public class DefaultFilterSelector : IFilterSelector
    {
        private readonly RssiPathLossModel _sharedModel;

        public DefaultFilterSelector(RssiPathLossModel sharedModel = null)
        {
            // Zdielany model garantuje, ze vsetky filtre pouzivaju rovnaku kalibraciu
            // (TxPower, PathLossExponent).
            _sharedModel = sharedModel ?? new RssiPathLossModel();
        }

        public ILocalizationFilter SelectFilter(IDeviceCapabilities capabilities)
        {
            if (capabilities.Tier == ComputeTier.High)
            {
                return new ParticleFilter(_sharedModel, particleCount: 500);
            }

            if (capabilities.Tier == ComputeTier.Medium)
            {
                // Medium tier: EKF je dostatocne presny.
                return new ExtendedKalmanFilter(_sharedModel);
            }

            // Low tier: rovnaky EKF, ale menej doveruje jednotlivym meraniam
            return new ExtendedKalmanFilter(_sharedModel,
                processNoiseDistance: 0.03,
                processNoiseVelocity: 0.01,
                measurementNoise: 6.0);
        }
    }
}