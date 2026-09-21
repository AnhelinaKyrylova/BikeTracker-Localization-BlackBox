using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace BikeTracker.Analysis
{

    /// Nacita measurements.csv a filter_comparison_output.csv priamo z disku
    /// a vygeneruje z nich formatovany .xlsx report (3 hárky: Namerane_data,
    /// Vysledky_filtrov, Suhrn so vzorcami). Bez grafov.

    public static class ExcelReportGenerator
    {
        private static readonly XLColor HeaderFill = XLColor.FromArgb(0x1F, 0x4E, 0x78);

        private class MeasurementRecord
        {
            public int CisloMerania;
            public double VzdialenostM;
            public double RssiDbm;
            public string Prostredie = "";
            public string LosNlos = "";
            public string Poznamka = "";
            public string CasMerania = "";
        }

        private class FilterResultRecord
        {
            public double VzdialenostSkutocna;
            public int CisloVzorky;
            public double Rssi;
            public long DeltaMs;
            public double EkfOdhad;
            public double PfOdhad;
        }

        public static void GenerateFromCsv(string measurementsCsvPath, string filterOutputCsvPath, string outputXlsxPath, RssiCalibration.Result? calibration = null)
        {
            if (!File.Exists(measurementsCsvPath))
                throw new FileNotFoundException($"Súbor s meraniami nenájdený: {measurementsCsvPath}");
            if (!File.Exists(filterOutputCsvPath))
                throw new FileNotFoundException($"Súbor s výsledkami filtrov nenájdený: {filterOutputCsvPath}");

            var measurements = ReadMeasurements(measurementsCsvPath);
            var results = ReadFilterResults(filterOutputCsvPath);

            using var workbook = new XLWorkbook();

            AddNameraneData(workbook, measurements);
            int lastResultRow = AddVysledkyFiltrov(workbook, results);
            AddSuhrn(workbook, results, lastResultRow, calibration);

            workbook.SaveAs(outputXlsxPath);
        }

        // Citanie CSV

        private static List<MeasurementRecord> ReadMeasurements(string path)
        {
            var result = new List<MeasurementRecord>();
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
                    result.Add(new MeasurementRecord
                    {
                        CisloMerania = int.Parse(parts[0].Trim()),
                        VzdialenostM = ParseDouble(parts[1]),
                        RssiDbm = ParseDouble(parts[2]),
                        Prostredie = parts.Length > 3 ? parts[3].Trim() : "",
                        LosNlos = parts.Length > 4 ? parts[4].Trim() : "",
                        Poznamka = parts.Length > 5 ? parts[5].Trim() : "",
                        CasMerania = parts.Length > 6 ? parts[6].Trim() : ""
                    });
                }
                catch
                {
                    Console.WriteLine($"[Excel report] Riadok {i + 1} v {Path.GetFileName(path)} sa nepodarilo spracovať, preskočený.");
                }
            }

            return result;
        }

        private static List<FilterResultRecord> ReadFilterResults(string path)
        {
            var result = new List<FilterResultRecord>();
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return result;

            char delimiter = lines[0].Contains(';') ? ';' : ',';

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var parts = lines[i].Split(delimiter);
                if (parts.Length < 6) continue;

                try
                {
                    result.Add(new FilterResultRecord
                    {
                        VzdialenostSkutocna = ParseDouble(parts[0]),
                        CisloVzorky = int.Parse(parts[1].Trim()),
                        Rssi = ParseDouble(parts[2]),
                        DeltaMs = long.Parse(parts[3].Trim()),
                        EkfOdhad = ParseDouble(parts[4]),
                        PfOdhad = ParseDouble(parts[5])
                    });
                }
                catch
                {
                    Console.WriteLine($"[Excel report] Riadok {i + 1} v {Path.GetFileName(path)} sa nepodarilo spracovať, preskočený.");
                }
            }

            return result;
        }

        private static double ParseDouble(string s)
        {
            s = s.Trim().Replace(",", ".");
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        // Generovanie harkov
      
        private static void StyleHeaderRow(IXLRow row, int columnCount)
        {
            var range = row.Cells(1, columnCount);
            range.Style.Font.Bold = true;
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.FontName = "Arial";
            range.Style.Fill.BackgroundColor = HeaderFill;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.WrapText = true;
        }

        private static void ApplyBorders(IXLRange range)
        {
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.FromArgb(0xB7, 0xB7, 0xB7);
            range.Style.Border.InsideBorderColor = XLColor.FromArgb(0xB7, 0xB7, 0xB7);
            range.Style.Font.FontName = "Arial";
            range.Style.Font.FontSize = 10;
        }

        private static void AddNameraneData(XLWorkbook workbook, List<MeasurementRecord> measurements)
        {
            var ws = workbook.Worksheets.Add("Namerane_data");

            string[] headers = { "Cislo merania", "Vzdialenost (m)", "RSSI (dBm)", "Prostredie", "LOS/NLOS", "Poznamka", "Cas merania" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            StyleHeaderRow(ws.Row(1), headers.Length);

            int r = 2;
            foreach (var m in measurements)
            {
                ws.Cell(r, 1).Value = m.CisloMerania;
                ws.Cell(r, 2).Value = m.VzdialenostM;
                ws.Cell(r, 3).Value = m.RssiDbm;
                ws.Cell(r, 4).Value = m.Prostredie;
                ws.Cell(r, 5).Value = m.LosNlos;
                ws.Cell(r, 6).Value = m.Poznamka;
                ws.Cell(r, 7).Value = m.CasMerania;
                r++;
            }

            ApplyBorders(ws.Range(1, 1, r - 1, headers.Length));

            ws.Column(1).Width = 14;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 12;
            ws.Column(4).Width = 12;
            ws.Column(5).Width = 10;
            ws.Column(6).Width = 26;
            ws.Column(7).Width = 14;
            ws.SheetView.FreezeRows(1);
        }

        /// cislo posledneho riadku s datami
        private static int AddVysledkyFiltrov(XLWorkbook workbook, List<FilterResultRecord> results)
        {
            var ws = workbook.Worksheets.Add("Vysledky_filtrov");

            string[] headers = { "Vzdialenost_Skutocna", "Cislo_Vzorky", "RSSI", "Delta_t_ms", "EKF_Odhad", "PF_Odhad", "Poradie" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];
            StyleHeaderRow(ws.Row(1), headers.Length);

            int r = 2;
            foreach (var res in results)
            {
                ws.Cell(r, 1).Value = res.VzdialenostSkutocna;
                ws.Cell(r, 2).Value = res.CisloVzorky;
                ws.Cell(r, 3).Value = res.Rssi;
                ws.Cell(r, 4).Value = res.DeltaMs;
                ws.Cell(r, 5).Value = res.EkfOdhad;
                ws.Cell(r, 6).Value = res.PfOdhad;
                ws.Cell(r, 7).FormulaA1 = "=ROW()-1";
                r++;
            }

            int lastRow = r - 1;
            ApplyBorders(ws.Range(1, 1, lastRow, headers.Length));

            ws.Column(1).Width = 20;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 10;
            ws.Column(4).Width = 14;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 12;
            ws.Column(7).Width = 10;
            ws.SheetView.FreezeRows(1);

            return lastRow;
        }

        private static void AddSuhrn(XLWorkbook workbook, List<FilterResultRecord> results, int lastResultRow, RssiCalibration.Result? calibration)
        {
            var ws = workbook.Worksheets.Add("Suhrn");

            int headerRow = 1;
            if (calibration != null)
            {
                ws.Cell(1, 1).Value = "Kalibracia modelu (metoda najmensich stvorcov, z realnych meraní):";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(2, 1).Value = "TxPower (RSSI @ 1 m)";
                ws.Cell(2, 2).Value = calibration.TxPower;
                ws.Cell(2, 2).Style.NumberFormat.Format = "0.00";
                ws.Cell(2, 3).Value = "dBm";
                ws.Cell(3, 1).Value = "PathLossExponent (n)";
                ws.Cell(3, 2).Value = calibration.PathLossExponent;
                ws.Cell(3, 2).Style.NumberFormat.Format = "0.000";
                ws.Cell(4, 1).Value = "Pocet vzoriek pouzitych na kalibraciu (bez 0 m)";
                ws.Cell(4, 2).Value = calibration.SampleCount;
                headerRow = 6; // medzera pred samotnou tabulkou
            }

            // Ciste priemery (AVERAGE) vedia byt zavadzajuce, ak EKF pri velkych
            // casovych medzerach medzi meraniami ide na 0. 
            // Preto tabulka popri priemere obsahuje aj:
            // Chybu (%) voci skutocnej vzdialenosti,
            // Pocet kolapsov EKF (vzorky s odhadom 0,00 m) vysvetluje, PRECO
            // je priemer EKF niekedy nizky/nerealisticky namiesto toho aby to
            // vyzeralo ako chyba vo vypocte.
            string[] headers = {
                "Vzdialenost (m)", "Pocet vzoriek", "Priemer RSSI (dBm)",
                "Priemer EKF odhad (m)", "Chyba EKF (%)", "Pocet EKF kolapsov (0,00 m)",
                "Priemer PF odhad (m)", "Chyba PF (%)"
            };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(headerRow, c + 1).Value = headers[c];
            StyleHeaderRow(ws.Row(headerRow), headers.Length);

            var distances = results.Select(x => x.VzdialenostSkutocna).Distinct().OrderBy(x => x).ToList();

            int r = headerRow + 1;
            foreach (var d in distances)
            {
                ws.Cell(r, 1).Value = d;

                // ak neskor upravime CSV a znova spustime, tabulka sa prepocita sama
                ws.Cell(r, 2).FormulaA1 =
                    $"=COUNTIF(Vysledky_filtrov!$A$2:$A${lastResultRow},A{r})";
                ws.Cell(r, 3).FormulaA1 =
                    $"=AVERAGEIF(Vysledky_filtrov!$A$2:$A${lastResultRow},A{r},Vysledky_filtrov!$C$2:$C${lastResultRow})";
                ws.Cell(r, 4).FormulaA1 =
                    $"=AVERAGEIF(Vysledky_filtrov!$A$2:$A${lastResultRow},A{r},Vysledky_filtrov!$E$2:$E${lastResultRow})";
                ws.Cell(r, 5).FormulaA1 =
                    $"=IF(A{r}=0,\"-\",ABS(D{r}-A{r})/A{r}*100)";
                ws.Cell(r, 6).FormulaA1 =
                    $"=COUNTIFS(Vysledky_filtrov!$A$2:$A${lastResultRow},A{r},Vysledky_filtrov!$E$2:$E${lastResultRow},0)";
                ws.Cell(r, 7).FormulaA1 =
                    $"=AVERAGEIF(Vysledky_filtrov!$A$2:$A${lastResultRow},A{r},Vysledky_filtrov!$F$2:$F${lastResultRow})";
                ws.Cell(r, 8).FormulaA1 =
                    $"=IF(A{r}=0,\"-\",ABS(G{r}-A{r})/A{r}*100)";

                ws.Cell(r, 3).Style.NumberFormat.Format = "0.0";
                ws.Cell(r, 4).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 5).Style.NumberFormat.Format = "0.0";
                ws.Cell(r, 7).Style.NumberFormat.Format = "0.00";
                ws.Cell(r, 8).Style.NumberFormat.Format = "0.0";

                // Vizualne zvyraznenie stlpca s poctom kolapsov EKF (diagnosticky udaj)
                ws.Cell(r, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(0xFF, 0xE0, 0xE0);

                r++;
            }

            int lastRow = r - 1;
            ApplyBorders(ws.Range(headerRow, 1, lastRow, headers.Length));

            ws.Column(1).Width = 14;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 20;
            ws.Column(5).Width = 14;
            ws.Column(6).Width = 24;
            ws.Column(7).Width = 18;
            ws.Column(8).Width = 14;
            ws.SheetView.FreezeRows(headerRow);

            workbook.RecalculateAllFormulas();
        }
    }
}