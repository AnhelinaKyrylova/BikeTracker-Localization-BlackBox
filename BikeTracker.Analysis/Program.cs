using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BikeTracker.Core;
using BikeTracker.Core.Filters;
using BikeTracker.Core.Interfaces;
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
 
    /// Analyticky nastroj pouziva LocalizationBlackBox (nie priamo
    /// ExtendedKalmanFilter/ParticleFilter), aby sa na realnych datach
    /// overila ta ista architektura (IFilterSelector/ILocalizationFilter),
    /// aku pouziva aj appka BikeTracker. Parametre filtrov su schvalne
    /// zachovane presne take, ake boli pouzite pri povodnej analyze
    /// , aby sa vysledky nezmenili.
    public class AnalysisFilterSelector : IFilterSelector
    {
        private readonly RssiPathLossModel _model;
        private readonly bool _useParticleFilter;
 
        public AnalysisFilterSelector(RssiPathLossModel model, bool useParticleFilter)
        {
            _model = model;
            _useParticleFilter = useParticleFilter;
        }
 
        public ILocalizationFilter SelectFilter(IDeviceCapabilities capabilities)
        {
            if (_useParticleFilter)
                return new ParticleFilter(_model, particleCount: 500, measurementNoiseStd: 7.0, randomSeed: 42);
 
            return new ExtendedKalmanFilter(_model, measurementNoise: 7.0);
        }
    }
 
    /// Jednoduchy stopky na meranie vypoctovej narocnosti.
    public class CostTracker
    {
        public string Name { get; }
        public int CallCount { get; private set; }
        public long ElapsedTicks { get; private set; }
        public long AllocatedBytes { get; private set; }
 
        public CostTracker(string name) => Name = name;
 
        public PositionEstimate Measure(Func<PositionEstimate> action)
        {
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            var result = action();
            sw.Stop();
            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
 
            CallCount++;
            ElapsedTicks += sw.ElapsedTicks;
            AllocatedBytes += Math.Max(0, allocAfter - allocBefore);
            return result;
        }
 
        public double AvgMicroseconds => CallCount == 0 ? 0 :
            (ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency) / CallCount;
 
        public double AvgAllocatedBytes => CallCount == 0 ? 0 : (double)AllocatedBytes / CallCount;
 
        public void PrintSummary()
        {
            Console.WriteLine(
                $"  {Name,-20} priemerny cas: {AvgMicroseconds,8:F2} us/vzorka   " +
                $"priemerna alokacia: {AvgAllocatedBytes,8:F0} B/vzorka   ({CallCount} vzoriek)");
        }
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
 
            var ekfCost = new CostTracker("EKF (Low-tier)");
            var pfCost = new CostTracker("PF (High-tier)");
 
            // Capabilities sa tu realne nepouzivaju na rozhodovanie (AnalysisFilterSelector
            // vzdy vrati rovnaky typ filtra podla useParticleFilter), ale musia sa odovzdat,
            // lebo su sucastou kontraktu IFilterSelector/LocalizationBlackBox.
            var highTierCaps = DeviceCapabilitiesProfile.HighTierPhone();
            var lowTierCaps = DeviceCapabilitiesProfile.LowTierEmbedded();
 
            foreach (var group in groups)
            {
                var ekfBlackBox = new LocalizationBlackBox(lowTierCaps, new AnalysisFilterSelector(model, useParticleFilter: false));
                var pfBlackBox = new LocalizationBlackBox(highTierCaps, new AnalysisFilterSelector(model, useParticleFilter: true));
 
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
                        if (deltaMs <= 0) deltaMs = 500;
                    }
                    else if (row.CasMerania.HasValue)
                    {
                        deltaMs = 0;
                    }
                    else
                    {
                        deltaMs = 500;
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
 
                    var ekfEstimate = ekfCost.Measure(() => ekfBlackBox.Process(sample));
                    var pfEstimate = pfCost.Measure(() => pfBlackBox.Process(sample));
 
                    outputLines.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0};{1};{2};{3};{4:F2};{5:F2}",
                        row.VzdialenostM, sampleIndex, row.RssiDbm, deltaMs,
                        ekfEstimate.DistanceM, pfEstimate.DistanceM));
                }
 
                string timingNote = usedFallbackTiming ? " (POZOR: niektoré časy sa nepodarilo naparsovať, použitý fallback 500ms)" : "";
                Console.WriteLine($"Vzdialenosť {group.Key}m: {groupRows.Count} vzoriek spracovaných.{timingNote}");
            }
 
            Console.WriteLine();
            Console.WriteLine("Vypoctova narocnost (bod 5 zadania, namerane na tomto pocitaci):");
            ekfCost.PrintSummary();
            pfCost.PrintSummary();
            double ratio = pfCost.AvgMicroseconds / Math.Max(ekfCost.AvgMicroseconds, 0.0001);
            Console.WriteLine($"  Particle Filter je v priemere {ratio:F1}x narocnejsi na cas nez EKF.");
            Console.WriteLine();
 
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
 
            if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out var fallback))
                return fallback;
 
            return null;
        }
    }
}