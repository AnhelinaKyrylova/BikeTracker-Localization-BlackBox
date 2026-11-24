using System;
using BikeTracker.Core.Models;

namespace BikeTracker.Core.Filters
{

    /// Odhaduje smer k BLE tagu metodou "RSSI sweep".

    /// Telefon nema hardver na AoA (Angle of Arrival), takze smer sa z jedneho
    /// merania zistit neda. Pouzivatel preto pomaly otoci telefonom o 360 stupnov
    /// a pocas toho sa zbieraju dvojice (kompasovy smer, RSSI). Smer, pri ktorom
    /// bolo RSSI najsilnejsie, je odhadom smeru k tagu.

    /// Postup: kruh sa rozdeli na kose rovnakej sirky, v kazdom kosi sa drzi
    /// najsilnejsie RSSI a po sweepe sa vyberie najlepsi kos.

    public class RssiHeadingEstimator
    {
        private readonly int _binCount;
        private readonly double _binWidthDeg;
        private readonly double _peakSharpnessScaleDb;

        // najsilnejsie RSSI v kazdom kosi, null = do kosa nespadla ziadna vzorka
        private readonly double?[] _bestRssiPerBin;

        /// "binCount" Pocet kosov (36 = 10 stupnov na kos), minimum 4.
        /// O kolko dB musi byt najlepsi kos silnejsi nez priemer kosov, aby mala
        /// ostrost maxima plnu istotu (1.0).

        public RssiHeadingEstimator(int binCount = 36, double peakSharpnessScaleDb = 8.0)
        {
            if (binCount < 4)
                throw new ArgumentException("binCount musi byt aspon 4.", "binCount");

            _binCount = binCount;
            _binWidthDeg = 360.0 / binCount;
            _peakSharpnessScaleDb = peakSharpnessScaleDb;
            _bestRssiPerBin = new double?[binCount];
        }

        /// Zahodi vsetky zozbierane vzorky (zaciatok noveho sweepu)
        public void Reset()
        {
            for (int i = 0; i < _binCount; i++)
                _bestRssiPerBin[i] = null;
        }


        /// Prida jednu vzorku zo sweepu.
        /// compassHeadingDeg je smer telefonu (0-360, 0 = sever),
        /// rssi je namerana hodnota v danom okamihu.

        public void AddSample(double compassHeadingDeg, double rssi)
        {
            int bin = GetBinIndex(compassHeadingDeg);
            double? current = _bestRssiPerBin[bin];

            if (current == null || rssi > current.Value)
                _bestRssiPerBin[bin] = rssi;
        }


        /// Vypocita odhad smeru zo vsetkych doterajsich vzoriek.
        /// Confidence = ostrost maxima x pokrytie kruhu (obe v rozsahu 0-1).
        /// Pokrytie = 1 - (najvacsi prazdny usek kruhu / 360), takze riedky, ale
        /// rovnomerny sweep (malo paketov za sekundu) nie je trestany tak ako polovicny.

        public HeadingEstimate GetEstimate()
        {
            int filledBins = 0;
            int bestBin = -1;
            double bestRssi = double.NegativeInfinity;
            double sum = 0;

            for (int i = 0; i < _binCount; i++)
            {
                double? value = _bestRssiPerBin[i];
                if (value == null) continue;

                filledBins++;
                sum += value.Value;

                if (value.Value > bestRssi)
                {
                    bestRssi = value.Value;
                    bestBin = i;
                }
            }

            if (filledBins == 0)
            {
                return new HeadingEstimate
                {
                    HeadingDeg = 0,
                    Confidence = 0,
                    FilledBins = 0,
                    TotalBins = _binCount
                };
            }

            double meanRssi = sum / filledBins;

            // ostrost maxima
            double sharpness = Clamp01((bestRssi - meanRssi) / _peakSharpnessScaleDb);

            // ak sweep nepokryl cely kruh, istota klesa
            double coverage = ComputeCoverage();

            return new HeadingEstimate
            {
                HeadingDeg = (bestBin + 0.5) * _binWidthDeg, // stred najlepsieho kosa
                Confidence = sharpness * coverage,
                FilledBins = filledBins,
                TotalBins = _binCount
            };
        }

        /// Pokrytie kruhu: 1 - (najvacsi suvisly prazdny usek / cely kruh).
        /// Plny kruh = 1.0, jediny kos = 1 / binCount, polovicny sweep ~0.5.
        private double ComputeCoverage()
        {
            int first = -1;
            int previous = -1;
            int largestEmpty = 0;

            for (int i = 0; i < _binCount; i++)
            {
                if (_bestRssiPerBin[i] == null) continue;

                if (first < 0)
                    first = i;
                else
                    largestEmpty = Math.Max(largestEmpty, i - previous - 1);

                previous = i;
            }

            if (first < 0) return 0;

            // kruhovy uzatvarajuci usek: od posledneho vyplneneho kosa cez 360/0 k prvemu
            int wrapEmpty = (first + _binCount) - previous - 1;
            largestEmpty = Math.Max(largestEmpty, wrapEmpty);

            return 1.0 - (double)largestEmpty / _binCount;
        }

        private int GetBinIndex(double headingDeg)
        {
            // normalizacia do rozsahu 0-360 
            double normalized = headingDeg % 360.0;
            if (normalized < 0) normalized += 360.0;

            int bin = (int)(normalized / _binWidthDeg);

            // ochrana pred zaokruhlovacou chybou tesne pod 360
            if (bin >= _binCount) bin = _binCount - 1;

            return bin;
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}