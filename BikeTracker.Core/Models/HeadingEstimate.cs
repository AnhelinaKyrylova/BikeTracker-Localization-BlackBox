namespace BikeTracker.Core.Models
{

    /// Vysledok odhadu smeru k tagu, ziskany metodou RSSI sweep + kompas
    /// (Filters/RssiHeadingEstimator)

    public class HeadingEstimate
    {
        public double HeadingDeg { get; set; }
        public double Confidence { get; set; }
        public int FilledBins { get; set; }
        public int TotalBins { get; set; }
    }
}