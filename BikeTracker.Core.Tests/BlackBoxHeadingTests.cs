using System;
using BikeTracker.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
 
namespace BikeTracker.Core.Tests
{
    [TestClass]
    public class BlackBoxHeadingTests
    {
        [TestMethod]
        public void BlackBox_RecoversHeading_ThroughFacade_NotJustStandaloneEstimator()
        {
            var blackBox = new LocalizationBlackBox(DeviceCapabilitiesProfile.HighTierPhone());
 
            double trueHeadingDeg = 200.0;
            double maxRssi = -50.0;
            double attenuationPerDeg = 0.15;
 
            blackBox.StartHeadingSweep();
 
            for (int deg = 0; deg < 360; deg++)
            {
                double angularDistance = Math.Min(
                    Math.Abs(deg - trueHeadingDeg),
                    360.0 - Math.Abs(deg - trueHeadingDeg));
                double rssi = maxRssi - attenuationPerDeg * angularDistance;
                blackBox.AddHeadingSample(deg, rssi);
            }
 
            var result = blackBox.GetHeadingEstimate();
 
            double error = Math.Min(
                Math.Abs(result.HeadingDeg - trueHeadingDeg),
                360.0 - Math.Abs(result.HeadingDeg - trueHeadingDeg));
 
            Assert.IsLessThan(15.0, error,
                $"Black box mal cez facade vratit smer ~{trueHeadingDeg} deg, vratil {result.HeadingDeg} deg");
        }
    }
}