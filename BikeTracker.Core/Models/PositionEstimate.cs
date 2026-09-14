namespace BikeTracker.Core.Models
{
    public class PositionEstimate
    {
        public double DistanceM { get; set; }
        public double HeadingDeg { get; set; }
        public double Confidence { get; set; }
    }
}