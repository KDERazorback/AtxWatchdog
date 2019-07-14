using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Dumps generates ATX PSU Stats to streams
    /// </summary>
    public class AtxStatsDumper
    {
        public string OutputPrefix { get; set; }
        public string OutputFilename { get; set; }
        public bool AppendMode { get; set; } = false;

        public Stream ExtraData { get; set; }
        public string ExtraDataName { get; set; }

        public KeyValuePair<string, string>[] ExtraDirectories { get; set; }

        public AtxStatsDumper(string outputFilename)
        {
            FileInfo fi = new FileInfo(outputFilename);
            if (fi.Exists)
                AppendMode = true;

            OutputFilename = outputFilename;
            OutputPrefix = fi.FullName.Substring(0, fi.FullName.Length - fi.Extension.Length) + "_";
        }

        public void DumpCsvs(AtxStats stats)
        {
            // Print simplified curves from Edge data
            WriteCsv(OutputPrefix + "v12_simplified_curve.csv", stats.V12Stats.Points, stats.V12Stats.Edges);
            WriteCsv(OutputPrefix + "v5_simplified_curve.csv", stats.V5Stats.Points, stats.V5Stats.Edges);
            WriteCsv(OutputPrefix + "v5sb_simplified_curve.csv", stats.V5SBStats.Points, stats.V5SBStats.Edges);
            WriteCsv(OutputPrefix + "v3_3_simplified_curve.csv", stats.V3_3Stats.Points, stats.V3_3Stats.Edges);

            // Print T2 curve fitting models from each rail
            stats.V12Stats.T2StageStats?.SerializeTo(OutputPrefix + "v12_curve_fit.xml");
            stats.V5Stats.T2StageStats?.SerializeTo(OutputPrefix + "v5_curve_fit.xml");
            stats.V5SBStats.T2StageStats?.SerializeTo(OutputPrefix + "v5sb_curve_fit.xml");
            stats.V3_3Stats.T2StageStats?.SerializeTo(OutputPrefix + "v3_3_curve_fit.xml");

            // Print ON stats from each rail
            stats.V12Stats.OnStageStats?.SerializeTo(OutputPrefix + "v12_on.xml");
            stats.V5Stats.OnStageStats?.SerializeTo(OutputPrefix + "v5_on.xml");
            stats.V5SBStats.OnStageStats?.SerializeTo(OutputPrefix + "v5sb_on.xml");
            stats.V3_3Stats.OnStageStats?.SerializeTo(OutputPrefix + "v3_3_on.xml");

            // Write serialized data
            stats.V12Stats.SerializeTo(OutputPrefix + "v12_stats.xml");
            stats.V5Stats.SerializeTo(OutputPrefix + "v5_stats.xml");
            stats.V5SBStats.SerializeTo(OutputPrefix + "v5sb_stats.xml");
            stats.V3_3Stats.SerializeTo(OutputPrefix + "v3_3_stats.xml");
            stats.SerializeTo(OutputPrefix + "stats.xml");

            // Write extra data stream
            if (ExtraData != null) WriteRaw(OutputPrefix + ExtraDataName, ExtraData);

            // Write device metadata if present
            if (stats.DeviceInfo != null) stats.DeviceInfo.SerializeTo(OutputPrefix + "deviceinfo.xml");

            // Write final CSV data
            WriteCsv(OutputPrefix + "entry.csv", false, GetCsvEntryColumnNames());
            WriteCsv(OutputPrefix + "entry.csv", true,GetCsvEntryDataValues(stats));
        }

        public void DumpGzipped(AtxStats stats)
        {
            DumpGzipped(OutputPrefix + "package.tar.gz", stats);
        }

        public void DumpGzipped(string filename, AtxStats stats)
        {
            MemoryStream mem = new MemoryStream();
            TarOutputStream archive = new TarOutputStream(mem);
            archive.IsStreamOwner = false;

            // Print simplified curves from Edge data
            WriteTar(archive, "v12_simplified_curve.csv", stats.V12Stats.Points, stats.V12Stats.Edges);
            WriteTar(archive, "v5_simplified_curve.csv", stats.V5Stats.Points, stats.V5Stats.Edges);
            WriteTar(archive, "v5sb_simplified_curve.csv", stats.V5SBStats.Points, stats.V5SBStats.Edges);
            WriteTar(archive, "v3_3_simplified_curve.csv", stats.V3_3Stats.Points, stats.V3_3Stats.Edges);

            // Write serialized data
            WriteTar(archive, "v12_stats.xml", stats.V12Stats.SerializeToArray());
            WriteTar(archive, "v5_stats.xml", stats.V5Stats.SerializeToArray());
            WriteTar(archive, "v5sb_stats.xml", stats.V5SBStats.SerializeToArray());
            WriteTar(archive, "v3_3_stats.xml", stats.V3_3Stats.SerializeToArray());
            WriteTar(archive, "stats.xml", stats.SerializeToArray());

            // Print T2 curve fitting models from each rail
            if (stats.V12Stats.T2StageStats != null) WriteTar(archive, "v12_curve_fit.xml", stats.V12Stats.T2StageStats.SerializeToArray());
            if (stats.V5Stats.T2StageStats != null) WriteTar(archive, "v5_curve_fit.xml", stats.V5Stats.T2StageStats.SerializeToArray());
            if (stats.V5SBStats.T2StageStats != null) WriteTar(archive, "v5sb_curve_fit.xml", stats.V5SBStats.T2StageStats.SerializeToArray());
            if (stats.V3_3Stats.T2StageStats != null) WriteTar(archive, "v3_3_curve_fit.xml", stats.V3_3Stats.T2StageStats.SerializeToArray());

            // Print ON stats from each rail
            if (stats.V12Stats.OnStageStats != null) WriteTar(archive, "v12_on.xml", stats.V12Stats.OnStageStats.SerializeToArray());
            if (stats.V5Stats.OnStageStats != null) WriteTar(archive, "v5_on.xml", stats.V5Stats.OnStageStats.SerializeToArray());
            if (stats.V5SBStats.OnStageStats != null) WriteTar(archive, "v5sb_on.xml", stats.V5SBStats.OnStageStats.SerializeToArray());
            if (stats.V3_3Stats.OnStageStats != null) WriteTar(archive, "v3_3_on.xml", stats.V3_3Stats.OnStageStats.SerializeToArray());

            // Write device metadata if present
            if (stats.DeviceInfo != null) WriteTar(archive, "deviceinfo.xml", stats.DeviceInfo.SerializeToArray());

            // Write extra data stream
            if (ExtraData != null) WriteTar(archive, ExtraDataName, ExtraData);

            // Write extra data directories
            if (ExtraDirectories != null) WriteTarDirectories(archive, ExtraDirectories);

            // Write final CSV data
            using (MemoryStream mem2 = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(mem2, new UTF8Encoding(false), 4 * 1024, true))
                {
                    string csv = StringArrayToCsv(GetCsvEntryColumnNames());
                    writer.WriteLine(csv);
                    csv = StringArrayToCsv(GetCsvEntryDataValues(stats));
                    writer.WriteLine(csv);
                    writer.Flush();
                }

                mem2.Position = 0;
                WriteTar(archive, "entry.csv", mem2);
            }

            // Close archive
            archive.Finish();
            archive.Flush();
            archive.Dispose();

            // GZip archive
            mem.Position = 0;
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (GZipStream gzip = new GZipStream(fs, CompressionLevel.Optimal))
                {
                    byte[] buffer = new byte[32 * 1024];
                    int size = mem.Read(buffer, 0, buffer.Length);
                    while (size > 0)
                    {
                        gzip.Write(buffer, 0, size);
                        size = mem.Read(buffer, 0, buffer.Length);
                    }
                }
            }
        }

        protected void WriteRaw(string filename, Stream data)
        {
            data.Position = 0;
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                data.CopyTo(fs);
        }

        protected void WriteCsv(string filename, bool append, params string[] cells)
        {
            using (FileStream fs = new FileStream(filename, append ? FileMode.Append : FileMode.Create,
                FileAccess.Write, FileShare.None))
            {
                using (TextWriter writer = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    string line = StringArrayToCsv(cells);
                    writer.WriteLine(line);
                }
            }
        }

        protected void WriteCsv(string filename, float[] pointsX, long[] pointersY)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                WriteStream(fs, pointsX, pointersY);
        }

        protected void WriteStream(Stream stream, float[] pointsX, long[] pointersY)
        {
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4 * 1024, true))
            {
                for (var i = 0; i < pointersY.Length; i++)
                {
                    long p = pointersY[i];
                    if (i > 0)
                        writer.WriteLine();
                    writer.Write(pointersY[i]);
                    writer.Write(',');
                    writer.Write(pointsX[pointersY[i]]);
                }
            }
        }

        protected void WriteTar(TarOutputStream archive, string filename, Stream data)
        {
            data.Position = 0;

            TarEntry entry = TarEntry.CreateTarEntry(filename);
            entry.ModTime = DateTime.Now;
            entry.Size = data.Length - data.Position;

            archive.PutNextEntry(entry);
            data.CopyTo(archive);
            archive.CloseEntry();
        }

        protected void WriteTar(TarOutputStream archive, string filename, float[] pointsX, long[] pointersY)
        {
            TarEntry entry = TarEntry.CreateTarEntry(filename);
            MemoryStream mem = new MemoryStream();
            WriteStream(mem, pointsX, pointersY);

            mem.Position = 0;
            entry.ModTime = DateTime.Now;
            entry.Size = mem.Length;
            
            archive.PutNextEntry(entry);
            WriteStream(archive, pointsX, pointersY);
            archive.CloseEntry();
        }

        protected void WriteTar(TarOutputStream archive, string filename, byte[] buffer)
        {
            TarEntry entry = TarEntry.CreateTarEntry(filename);
            entry.ModTime = DateTime.Now;
            entry.Size = buffer.Length;

            archive.PutNextEntry(entry);
            archive.Write(buffer, 0, (int)entry.Size);
            archive.CloseEntry();
        }

        protected void WriteTarDirectories(TarOutputStream archive, KeyValuePair<string, string>[] directories)
        {
            foreach (KeyValuePair<string, string> directory in directories)
            {
                DirectoryInfo di = new DirectoryInfo(directory.Value);
                FileInfo[] files = di.GetFiles("*", SearchOption.AllDirectories);

                foreach (FileInfo fi in files)
                {
                    string name = ToRelativePath(fi.FullName, di.FullName);

                    FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

                    TarEntry entry = TarEntry.CreateTarEntry(name);
                    entry.ModTime = fi.CreationTime;
                    entry.Size = fs.Length;

                    archive.PutNextEntry(entry);
                    fs.CopyTo(archive);
                    archive.CloseEntry();
                }
            }
        }

        protected string ToRelativePath(string absolute, string basepath)
        {
            var a = new Uri(absolute);
            var b = new Uri(basepath);
            return b.MakeRelativeUri(a).ToString();
        }

        protected string[] GetCsvEntryColumnNames()
        {
            return new string[]
            {
                "Brand",
                "Model",
                "Serial Number",
                "Manufacture Year",
                "Test Date",
                "Is Good",
                "Final State",
                "+5VSB On Mean",
                "+5VSB On Deviation",
                "+12V On Mean",
                "+12V On Deviation",
                "+5V On Mean",
                "+5V On Deviation",
                "+3.3 On Mean",
                "+3.3 On Deviation",
                "+12V T2 Slope",
                "+5V T2 Slope",
                "+3.3V T2 Slope",
                "+5VSB Max V",
                "+512V Max V",
                "+5V Max V",
                "+3.3V Max V",
                "PG_GOOD Delay"
            };
        }

        protected string[] GetCsvEntryDataValues(AtxStats stats)
        {
            return new string[]
            {
                // INFO
                stats.DeviceInfo?.Brand ?? "N/A",
                stats.DeviceInfo?.Model ?? "N/A",
                stats.DeviceInfo?.SerialNumber ?? "N/A",
                stats.DeviceInfo?.ManufactureYear.ToString() ?? "N/A",
                stats.DeviceInfo?.TestDate.ToString() ?? "N/A",
                stats.DeviceInfo != null ? (stats.DeviceInfo.IsGood ? "GOOD" : "BAD") : "N/A",
                string.IsNullOrWhiteSpace(stats.LastStageRecorded) ? "T0" : stats.LastStageRecorded,

                // ON
                stats.V5SBStats.OnStageStats.MeanVoltage.ToString("N3"),
                stats.V5SBStats.OnStageStats.DeviationVoltage.ToString("N3"),

                stats.V12Stats.OnStageStats.MeanVoltage.ToString("N3"),
                stats.V12Stats.OnStageStats.DeviationVoltage.ToString("N3"),

                stats.V5Stats.OnStageStats.MeanVoltage.ToString("N3"),
                stats.V5Stats.OnStageStats.DeviationVoltage.ToString("N3"),

                stats.V3_3Stats.OnStageStats.MeanVoltage.ToString("N3"),
                stats.V3_3Stats.OnStageStats.DeviationVoltage.ToString("N3"),

                // T2
                stats.V12Stats.T2StageStats.Slope.ToString("N2"),
                stats.V5Stats.T2StageStats.Slope.ToString("N2"),
                stats.V3_3Stats.T2StageStats.Slope.ToString("N2"),

                // Max voltage per rail
                stats.V5SBStats.MaxVoltage.ToString("N3"),
                stats.V12Stats.MaxVoltage.ToString("N3"),
                stats.V5Stats.MaxVoltage.ToString("N3"),
                stats.V3_3Stats.MaxVoltage.ToString("N3"),

                // PG_OK signal delay
                stats.PgOkSignalTimeUs.ToString()
            };
        }

        protected string StringArrayToCsv(params string[] cells)
        {
            StringBuilder writer = new StringBuilder();

            for (var i = 0; i < cells.Length; i++)
            {
                if (i > 0)
                    writer.Append(',');
                writer.Append('"');
                writer.Append(cells[i].Replace("\"", "\"\""));
                writer.Append('"');
            }
            writer.AppendLine();

            return writer.ToString();
        }
    }
}
