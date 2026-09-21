using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BikeTracker.Core.Filters;
using BikeTracker.Core.Models;

namespace BikeTracker.Analysis
{
    public class MeasurementRow
    {
        public int CisloMerania { get; set; }
        public double VzdialenostM { get; set; }
        public double RssiDbm { get; set; }
        public string Prostredie { get; set; } = "";
        public string LosNlos { get; set; } = "";
        public string Poznamka { get; set; } = "";
        public TimeSpan? CasMerania { get; set; } // null, ak sa cas nepodarilo naparsovať
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            string inputPath = args.Length > 0 ? args[0] : "measurements.csv";

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Súbor '{inputPath}' nenájdený. Skopíruj CSV export z Excelu do tohto priečinka.");
                return;
            }

            var rows = ReadCsv(inputPath);
            Console.WriteLine($"Načítaných {rows.Count} riadkov meraní.");

            var groups = rows.GroupBy(r => r.VzdialenostM).OrderBy(g => g.Key);

            var outputLines = new List<string> { "Vzdialenost_Skutocna;Cislo_Vzorky;RSSI;Delta_t_ms;EKF_Odhad;PF_Odhad" };

            // Kalibracia TxPower/PathLossExponent sa pocita automaticky metodou
            // najmensich stvorcov priamo z nacitanych meraní (RssiCalibration.cs),
            // takze sa vzdy odzrkadluje aktualny obsah measurements.csv bez
            // potreby rucne prepisovat cisla v kode.
            var calibration = RssiCalibration.Fit(rows);
            Console.WriteLine(
                $"Kalibracia: TxPower={calibration.TxPower:F2} dBm, " +
                $"PathLossExponent={calibration.PathLossExponent:F3} " +
                $"(z {calibration.SampleCount} vzoriek, bez 0 m).");

            var model = new RssiPathLossModel
            {
                TxPower = calibration.TxPower,
                PathLossExponent = calibration.PathLossExponent
            };

            foreach (var group in groups)
            {
                var ekf = new ExtendedKalmanFilter(model, measurementNoise: 7.0);
                var pf = new ParticleFilter(model, particleCount: 500, measurementNoiseStd: 7.0, randomSeed: 42);
                var groupRows = group.OrderBy(r => r.CasMerania ?? TimeSpan.Zero).ToList();

                long t = 0;
                int sampleIndex = 0;
                TimeSpan? previousTime = null;
                bool usedFallbackTiming = false;

                foreach (var row in groupRows)
                {
                    sampleIndex++;

                    long deltaMs;
                    if (row.CasMerania.HasValue && previousTime.HasValue)
                    {
                        deltaMs = (long)(row.CasMerania.Value - previousTime.Value).TotalMilliseconds;
                        if (deltaMs <= 0) deltaMs = 500; // ochrana pri chybnom/duplicitnom case
                    }
                    else if (row.CasMerania.HasValue)
                    {
                        deltaMs = 0; // prva vzorka v skupine
                    }
                    else
                    {
                        deltaMs = 500; // cas sa nepodarilo naparsovat - fallback
                        usedFallbackTiming = true;
                    }

                    t += deltaMs;
                    previousTime = row.CasMerania ?? previousTime;

                    var sample = new RssiSample
                    {
                        DeviceId = "TAG-1",
                        Rssi = (int)Math.Round(row.RssiDbm),
                        TimestampMs = t
                    };

                    var ekfEstimate = ekf.Update(sample);
                    var pfEstimate = pf.Update(sample);

                    outputLines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0};{1};{2};{3};{4:F2};{5:F2}",
                        row.VzdialenostM, sampleIndex, row.RssiDbm, deltaMs,
                        ekfEstimate.DistanceM, pfEstimate.DistanceM));
                }

                string timingNote = usedFallbackTiming ? " (POZOR: niektoré časy sa nepodarilo naparsovať, použitý fallback 500ms)" : "";
                Console.WriteLine($"Vzdialenosť {group.Key}m: {groupRows.Count} vzoriek spracovaných.{timingNote}");

            }

            string outputPath = "filter_comparison_output.csv";
            File.WriteAllLines(outputPath, outputLines);
            Console.WriteLine($"Výsledky uložené do '{outputPath}'. Otvor v Exceli a vytvor graf.");
            string excelPath = "BikeTracker_Merania_Analyza.xlsx";
            ExcelReportGenerator.GenerateFromCsv(inputPath, outputPath, excelPath, calibration);
            Console.WriteLine($"Excel report uložený do '{excelPath}'.");
        }

        private static List<MeasurementRow> ReadCsv(string path)
        {
            var result = new List<MeasurementRow>();
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return result;

            char delimiter = lines[0].Contains(';') ? ';' : ',';

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var parts = lines[i].Split(delimiter);
                if (parts.Length < 3) continue;
                if (string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2])) continue;

                try
                {
                    result.Add(new MeasurementRow
                    {
                        CisloMerania = ParseInt(parts[0]),
                        VzdialenostM = ParseDouble(parts[1]),
                        RssiDbm = ParseDouble(parts[2]),
                        Prostredie = parts.Length > 3 ? parts[3] : "",
                        LosNlos = parts.Length > 4 ? parts[4] : "",
                        Poznamka = parts.Length > 5 ? parts[5] : "",
                        CasMerania = parts.Length > 6 ? ParseTime(parts[6]) : null
                    });
                }
                catch
                {
                    Console.WriteLine($"Riadok {i + 1} sa nepodarilo spracovať, preskočený: {lines[i]}");
                }
            }

            return result;
        }

        private static double ParseDouble(string s)
        {
            s = s.Trim().Replace(",", ".");
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string s) => int.Parse(s.Trim());

        // Podporuje formaty "14:32", "14:32:05", "14:32:05.500"
        private static TimeSpan? ParseTime(string s)
        {
            s = s.Trim();
            if (string.IsNullOrEmpty(s)) return null;

            string[] formats = { "hh\\:mm\\:ss\\.fff", "hh\\:mm\\:ss", "hh\\:mm" };
            foreach (var format in formats)
            {
                if (TimeSpan.TryParseExact(s, format, CultureInfo.InvariantCulture, out var result))
                    return result;
            }

            // posledny pokus 
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var fallback))
                return fallback;

            return null;
        }
    }
}