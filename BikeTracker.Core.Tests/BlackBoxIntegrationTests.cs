using System;
using BikeTracker.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeTracker.Core.Tests
{
    [TestClass]
    public class BlackBoxIntegrationTests
    {
        private static double AngularDistance(double a, double b)
        {
            double diff = Math.Abs(a - b) % 360.0;
            return diff > 180.0 ? 360.0 - diff : diff;
        }

        private static double Rssi(double headingDeg, double trueHeadingDeg) =>
            -50.0 - 0.15 * AngularDistance(headingDeg, trueHeadingDeg);

        [TestMethod]
        public void Heading_SparseButEvenSweep_HasUsableConfidence()
        {
            // realny sweep ma len par desiatok vzoriek (2-8 paketov/s), nie 360
            var box = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
            double trueHeading = 100.0;

            box.StartHeadingSweep();
            for (double deg = 0; deg < 360; deg += 25)
                box.AddHeadingSample(deg, Rssi(deg, trueHeading));

            HeadingEstimate heading = box.GetHeadingEstimate();

            Assert.IsLessThan(20.0, AngularDistance(heading.HeadingDeg, trueHeading));
            Assert.IsGreaterThan(0.3, heading.Confidence,
                "Rovnomerny, ale riedky sweep by nemal mat nizku istotu.");
        }

        [TestMethod]
        public void Heading_HalfCircleSweep_HasLowerConfidenceThanFullSweep()
        {
            var fullBox = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
            var halfBox = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
            double trueHeading = 100.0;

            fullBox.StartHeadingSweep();
            halfBox.StartHeadingSweep();

            for (int deg = 0; deg < 360; deg += 5)
            {
                fullBox.AddHeadingSample(deg, Rssi(deg, trueHeading));
                if (deg < 180) halfBox.AddHeadingSample(deg, Rssi(deg, trueHeading));
            }

            double full = fullBox.GetHeadingEstimate().Confidence;
            double half = halfBox.GetHeadingEstimate().Confidence;

            Assert.IsLessThan(full, half);
            Assert.IsLessThan(0.6, half);
        }

        [TestMethod]
        public void FilterName_ReflectsDeviceTier()
        {
            var high = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
            var low = new LocalizationBlackBox(DeviceCapabilitiesProfile.LowTierEmbedded());

            Assert.AreEqual("ParticleFilter", high.FilterName);
            Assert.AreEqual("ExtendedKalmanFilter", low.FilterName);
        }
    }
}