namespace BikeTracker.Core.Models
{
    public class RssiSample
    {
        public string DeviceId { get; set; }
        public int Rssi { get; set; }
        public long TimestampMs { get; set; }
        public int? TxPower { get; set; }
    }
}