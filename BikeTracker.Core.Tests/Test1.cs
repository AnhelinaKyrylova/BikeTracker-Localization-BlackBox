using System;
using BikeTracker.Core.Filters;
using BikeTracker.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeTracker.Core.Tests
{
    [TestClass]
    public class FilterSanityTests
    {
        [TestMethod]
        public void ExtendedKalmanFilter_ConvergesToStableDistance()
        {
            var model = new RssiPathLossModel { TxPower = -59, PathLossExponent = 2.5 };
            var filter = new ExtendedKalmanFilter(model);

            // Simulujeme tag, ktory stoji presne 5m od telefonu.
            // Ocakavane RSSI pri 5m: -59 - 10*2.5*log10(5) ≈ -76.5
            double trueDistance = 5.0;
            double expectedRssi = model.PredictRssi(trueDistance);

            var random = new Random(42);
            PositionEstimate? lastEstimate = null;
            long t = 0;

            for (int i = 0; i < 50; i++)
            {
                // pridame realisticky sum ±3 dB
                double noisyRssi = expectedRssi + (random.NextDouble() - 0.5) * 6.0;
                t += 200; // vzorka kazdych 200ms

                lastEstimate = filter.Update(new RssiSample
                {
                    DeviceId = "TAG-1",
                    Rssi = (int)Math.Round(noisyRssi),
                    TimestampMs = t
                });
            }
            
            Assert.IsNotNull(lastEstimate);
            Console.WriteLine($"Odhadovana vzdialenost po 50 vzorkach: {lastEstimate.DistanceM:F2} m (skutocna: {trueDistance} m)");
            // Filter by po 50 vzorkach mal konvergovat blizko k 5m (tolerancia 1m)
            Assert.IsLessThan(1.0, Math.Abs(lastEstimate.DistanceM - trueDistance),
                $"Ocakavana vzdialenost ~{trueDistance}m, filter vratil {lastEstimate.DistanceM}m");  
        }

        [TestMethod]
        public void ParticleFilter_ConvergesToStableDistance()
        {
            var model = new RssiPathLossModel { TxPower = -59, PathLossExponent = 2.5 };
            var filter = new ParticleFilter(model, particleCount: 500, randomSeed: 42);

            double trueDistance = 5.0;
            double expectedRssi = model.PredictRssi(trueDistance);

            var random = new Random(42);
            PositionEstimate? lastEstimate = null;
            long t = 0;

            for (int i = 0; i < 50; i++)
            {
                double noisyRssi = expectedRssi + (random.NextDouble() - 0.5) * 6.0;
                t += 200;

                lastEstimate = filter.Update(new RssiSample
                {
                    DeviceId = "TAG-1",
                    Rssi = (int)Math.Round(noisyRssi),
                    TimestampMs = t
                });
            }
            
            Assert.IsNotNull(lastEstimate);
            Console.WriteLine($"Odhadovana vzdialenost po 50 vzorkach: {lastEstimate.DistanceM:F2} m (skutocna: {trueDistance} m)");
            Assert.IsLessThan(1.0, Math.Abs(lastEstimate.DistanceM - trueDistance),
                $"Ocakavana vzdialenost ~{trueDistance}m, filter vratil {lastEstimate.DistanceM}m");
        }
    }
}
