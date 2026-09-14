using BikeTracker.Core.Interfaces;

namespace BikeTracker.Core.Filters
{
    /// <summary>
    /// Strategy/Factory: rozhoduje, ktory ILocalizationFilter sa nasadi,
    /// na zaklade deklarovanych schopnosti zariadenia.
    /// Toto je jadro bodu 3 zadania - skalovatelna voľba filtra.
    /// </summary>
    public class DefaultFilterSelector : IFilterSelector
    {
        private readonly RssiPathLossModel _sharedModel;

        public DefaultFilterSelector(RssiPathLossModel sharedModel = null)
        {
            // Zdielany model garantuje, ze vsetky filtre pouzivaju rovnaku kalibraciu
            // (TxPower, PathLossExponent) - dolezite pre porovnanie v bode 1.
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

            // Low tier: rovnaky EKF, ale konzervativnejsie parametre sumu
            // (menej doveruje jednotlivym meraniam, kedze MCU typicky nema priestor
            // na sofistikovanejsie predspracovanie RSSI pred filtrom).
            return new ExtendedKalmanFilter(_sharedModel,
                processNoiseDistance: 0.03,
                processNoiseVelocity: 0.01,
                measurementNoise: 6.0);
        }
    }
}