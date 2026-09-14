using System;

namespace BikeTracker.Core.Filters
{
    /// <summary>
    /// Spolocny meraci model: prevod RSSI - vzdialenost.
    /// Parametre (TxPower, PathLossExponent) su konfigurovatelne,
    /// aby sa dali neskor kalibrovat na realne namerane data z tagu.
    /// </summary>
    public class RssiPathLossModel
    {
        public double TxPower { get; set; } = -59;      // typicka hodnota RSSI @ 1m, treba kalibrovat
        public double PathLossExponent { get; set; } = 2.5; // 2.0-2.5 open urban, 2.7-3.5 NLOS

        // h(d) - ocakavane RSSI pri danej vzdialenosti
        public double PredictRssi(double distanceM)
        {
            if (distanceM < 0.01) distanceM = 0.01; // ochrana pred log(0)
            return TxPower - 10.0 * PathLossExponent * Math.Log10(distanceM);
        }

        // d(RSSI) - hruby priamy odhad vzdialenosti z RSSI (na inicializaciu filtrov)
        public double EstimateDistance(double rssi)
        {
            double exponent = (TxPower - rssi) / (10.0 * PathLossExponent);
            return Math.Pow(10.0, exponent);
        }

        // dh/dd - derivacia meracej funkcie podla vzdialenosti (Jacobian pre EKF)
        public double MeasurementJacobian(double distanceM)
        {
            if (distanceM < 0.01) distanceM = 0.01;
            return -10.0 * PathLossExponent / (distanceM * Math.Log(10.0));
        }
    }
}