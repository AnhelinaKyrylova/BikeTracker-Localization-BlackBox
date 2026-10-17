using System;
using BikeTracker.Core.Filters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeTracker.Core.Tests
{
    [TestClass]
    public class RssiHeadingEstimatorTests
    {
        [TestMethod]
        public void Estimator_RecoversTrueHeading_FromSimulatedSweep()
        {
            double trueHeadingDeg = 120.0;
            double maxRssi = -50.0;
            double attenuationPerDeg = 0.15; // dB na stupen odchylky od skutocneho smeru

            var estimator = new RssiHeadingEstimator(binCount: 36);
            var random = new Random(42);

            // simulacia sweepu: kazdy stupen 0-359 s malym sumom
            for (int deg = 0; deg < 360; deg++)
            {
                double angularDistance = AngularDistance(deg, trueHeadingDeg);
                double expectedRssi = maxRssi - attenuationPerDeg * angularDistance;
                double noisyRssi = expectedRssi + (random.NextDouble() - 0.5) * 4.0; // ±2 dB sum

                estimator.AddSample(deg, noisyRssi);
            }

            var result = estimator.GetEstimate();

            Console.WriteLine($"Odhadovany smer: {result.HeadingDeg:F1} deg (skutocny: {trueHeadingDeg} deg), confidence={result.Confidence:F2}");

            // tolerancia jeden a pol kosa (36 kosov = 10 stupnov na kos)
            double error = AngularDistance(result.HeadingDeg, trueHeadingDeg);
            Assert.IsLessThan(15.0, error,
                $"Ocakavany smer ~{trueHeadingDeg} deg, odhadnute {result.HeadingDeg} deg");
            Assert.IsGreaterThan(0.3, result.Confidence, "Confidence by mala byt rozumne vysoka pri jasnom maxime.");
        }

        [TestMethod]
        public void Estimator_ReturnsZeroConfidence_WhenNoSamples()
        {
            var estimator = new RssiHeadingEstimator();
            var result = estimator.GetEstimate();

            Assert.AreEqual(0.0, result.Confidence);
            Assert.AreEqual(0, result.FilledBins);
        }

        [TestMethod]
        public void Estimator_HandlesWraparound_AtZeroDegrees()
        {
            double trueHeadingDeg = 5.0; // blizko 0/360 hranice
            double maxRssi = -50.0;
            double attenuationPerDeg = 0.15;

            var estimator = new RssiHeadingEstimator(binCount: 36);

            for (int deg = 0; deg < 360; deg++)
            {
                double angularDistance = AngularDistance(deg, trueHeadingDeg);
                double expectedRssi = maxRssi - attenuationPerDeg * angularDistance;
                estimator.AddSample(deg, expectedRssi);
            }

            var result = estimator.GetEstimate();
            double error = AngularDistance(result.HeadingDeg, trueHeadingDeg);

            Assert.IsLessThan(15.0, error,
                $"Ocakavany smer ~{trueHeadingDeg} deg (wraparound), odhadnute {result.HeadingDeg} deg");
        }

        private static double AngularDistance(double a, double b)
        {
            double diff = Math.Abs(a - b) % 360.0;
            return diff > 180.0 ? 360.0 - diff : diff;
        }
    }
}