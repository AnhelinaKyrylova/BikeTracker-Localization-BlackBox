using System;
using System.Collections.Generic;
using System.Linq;

namespace BikeTracker.Analysis
{

    /// Vypocita parametre log-distance path loss modelu (TxPower, PathLossExponent)
    /// priamo z realnych merani metodou najmensich stvorcov (linearna regresia
    /// RSSI = TxPower - 10*n*log10(d), RSSI = A + B*log10(d), kde A=TxPower, n=-B/10).
    /// Merania so vzdialenostou 0 m sa vynechavaju.

    public static class RssiCalibration
    {
        public class Result
        {
            public double TxPower;
            public double PathLossExponent;
            public int SampleCount;
        }

        public static Result Fit(IEnumerable<MeasurementRow> rows)
        {
            var points = rows
                .Where(r => r.VzdialenostM > 0)
                .Select(r => (x: Math.Log10(r.VzdialenostM), y: r.RssiDbm))
                .ToList();

            if (points.Count < 2)
                throw new InvalidOperationException(
                    "Na kalibraciu su potrebne aspon 2 merania s nenulovou vzdialenostou.");

            double meanX = points.Average(p => p.x);
            double meanY = points.Average(p => p.y);

            double numerator = points.Sum(p => (p.x - meanX) * (p.y - meanY));
            double denominator = points.Sum(p => (p.x - meanX) * (p.x - meanX));

            double b = numerator / denominator;
            double a = meanY - b * meanX;

            return new Result
            {
                TxPower = a,
                PathLossExponent = -b / 10.0,
                SampleCount = points.Count
            };
        }
    }
}