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
            stats.V5Stats.T2StageStats?.SerializeTo(OutputPrefix + "v5sb_curve_fit.xml");
            stats.V3_3Stats.T2StageStats?.SerializeTo(OutputPrefix + "v3_3_curve_fit.xml");

            // Print ON stats from each rail
            stats.V12Stats.OnStageStats?.SerializeTo(OutputPrefix + "v12_on.xml");
            stats.V5Stats.OnStageStats?.SerializeTo(OutputPrefix + "v5_on.xml");
            stats.V5Stats.OnStageStats?.SerializeTo(OutputPrefix + "v5sb_on.xml");
            stats.V3_3Stats.OnStageStats?.SerializeTo(OutputPrefix + "v3_3_on.xml");

            // Write serialized data
            stats.V12Stats.SerializeTo(OutputPrefix + "v12_stats.xml");
            stats.V12Stats.SerializeTo(OutputPrefix + "v5_stats.xml");
            stats.V12Stats.SerializeTo(OutputPrefix + "v5sb_stats.xml");
            stats.V12Stats.SerializeTo(OutputPrefix + "v3_3_stats.xml");
            stats.SerializeTo(OutputPrefix + "stats.xml");

            // Write extra data stream
            if (ExtraData != null) WriteRaw(OutputPrefix + ExtraDataName, ExtraData);

            // Write device metadata if present
            if (stats.DeviceInfo != null) stats.DeviceInfo.SerializeTo(OutputPrefix + "deviceinfo.xml");
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
            WriteTar(archive, "v5_stats.xml", stats.V12Stats.SerializeToArray());
            WriteTar(archive, "v5sb_stats.xml", stats.V12Stats.SerializeToArray());
            WriteTar(archive, "v3_3_stats.xml", stats.V12Stats.SerializeToArray());
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
    }
}
