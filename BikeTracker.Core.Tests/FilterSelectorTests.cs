using BikeTracker.Core.Filters;
using BikeTracker.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeTracker.Core.Tests
{
    [TestClass]
    public class FilterSelectorTests
    {
        [TestMethod]
        public void Selector_ReturnsParticleFilter_ForHighTier()
        {
            var selector = new DefaultFilterSelector();
            var filter = selector.SelectFilter(DeviceCapabilitiesProfile.HighTierPhone());

            Assert.IsInstanceOfType(filter, typeof(ParticleFilter));
        }

        [TestMethod]
        public void Selector_ReturnsExtendedKalmanFilter_ForLowTier()
        {
            var selector = new DefaultFilterSelector();
            var filter = selector.SelectFilter(DeviceCapabilitiesProfile.LowTierEmbedded());

            Assert.IsInstanceOfType(filter, typeof(ExtendedKalmanFilter));
        }

        [TestMethod]
        public void BlackBox_ProcessesSampleAndReturnsEstimate_RegardlessOfTier()
        {
            var blackBoxHigh = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
            var blackBoxLow = new LocalizationBlackBox(DeviceCapabilitiesProfile.LowTierEmbedded());

            var sample = new RssiSample { DeviceId = "TAG-1", Rssi = -70, TimestampMs = 1000 };

            var estimateHigh = blackBoxHigh.Process(sample);
            var estimateLow = blackBoxLow.Process(sample);

            Assert.IsNotNull(estimateHigh);
            Assert.IsNotNull(estimateLow);
        }
    }
}