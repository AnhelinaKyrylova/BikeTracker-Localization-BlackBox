using System;
using BikeTracker.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeTracker.Core.Tests
{
    [TestClass]
    public class BlackBoxHeadingTests
    {
        [TestMethod]
        public void BlackBox_FillsHeading_AfterSweep()
        {
            var box = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
            double trueHeading = 200.0;

            box.StartHeadingSweep();
            for (int deg = 0; deg < 360; deg++)
            {
                double diff = Math.Abs(deg - trueHeading) % 360.0;
                double angular = diff > 180.0 ? 360.0 - diff : diff;
                box.AddHeadingSample(deg, -50.0 - 0.15 * angular);
            }

            HeadingEstimate heading = box.GetHeadingEstimate();
            PositionEstimate estimate = box.Process(
                new RssiSample { DeviceId = "T", Rssi = -70, TimestampMs = 1000 });

            Assert.AreEqual(heading.HeadingDeg, estimate.HeadingDeg, 0.001);
            Assert.IsLessThan(15.0, Math.Abs(heading.HeadingDeg - trueHeading));
        }
    }
}