﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Xml.Serialization;
using MathNet.Numerics;
using MathNet.Numerics.LinearRegression;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Analyzes ATX binary data and generates various PSU statistics
    /// </summary>
    public class AtxStaticAnalyzer
    {
        protected DateTime LogStartTime;

        protected const int POSITIVE = 1;
        protected const int NEGATIVE = -1;

        public delegate void LogEntryWrittenDelegate(AtxStaticAnalyzer instance, string message);
        public event LogEntryWrittenDelegate LogEntryWritten;

        /// <summary>
        /// Stores names for all ATX PSU stages that may be present in a metadata file
        /// </summary>
        public string[] MetadataLinesNames { get; set; } =
        {
            "T1",
            "T2",
            "T3",
            "ON",
            "T6",
            "OFF"
        };

        public float[] V12Points { get; set; }
        public float[] V5Points { get; set; }
        public float[] V5SBPoints { get; set; }
        public float[] V3_3Points { get; set; }

        public float[] TimeSerie { get; set; }
        public long[][] MetadataMarkers { get; set; }

        public Stream LogStream { get; protected set; }
        protected StreamWriter LogWriter { get; set; }

        public float PeakDetectionWindowSizePercent { get; set; } = 0.05f; // Relative to the total point count
        public float EdgeDetectionSensitivity { get; set; } = 0.05f; // Real value, not percent. (usually mV)

        protected void OpenLog()
        {
            LogStream = new MemoryStream();
            LogWriter = new StreamWriter(LogStream, new UTF8Encoding(false), 4 * 1024, true);

            LogStartTime = DateTime.Now;

            StringBuilder str = new StringBuilder();

            str.Append(Environment.MachineName);
            str.Append(" ");
            str.Append(Environment.OSVersion.ToString());
            str.Append(" (");
            str.Append(Environment.OSVersion.VersionString);
            str.Append(") ");

            string hostInfo = str.ToString();
            str.Clear();

            LogWriter.WriteLine("LOG FILE OPENED");
            LogWriter.WriteLine("Date: " + DateTime.Now.ToLongDateString());
            LogWriter.WriteLine("PeakDetectionWindowSize: " + (PeakDetectionWindowSizePercent * 100).ToString("N3") + "%");
            LogWriter.WriteLine("EdgeDetectionSensitivity: " + EdgeDetectionSensitivity.ToString("N3") + " units.");
            LogWriter.WriteLine("Hostinfo: {0}", hostInfo);
        }

        protected void CloseLog()
        {
            if (LogWriter == null)
                return;

            DateTime LogEndTime = DateTime.Now;
            TimeSpan duration = LogEndTime - LogStartTime;

            LogWriter.WriteLine("Entire process took: {0} days, {1} hours, {2} minutes, {3} seconds, {4} milliseconds.", duration.Days, duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds); ;

            LogWriter.WriteLine("LOG FILE CLOSED");
            LogWriter.Flush();
            LogWriter.Dispose();
        }

        protected void WriteLog(string log)
        {
            OnLogEntryWritten(log);

            if (LogWriter != null)
                LogWriter.WriteLine(log);
        }

        protected void WriteLog(string format, params string[] args)
        {
            OnLogEntryWritten(string.Format(format, args));

            if (LogWriter != null)
                LogWriter.WriteLine(format, args);
        }

        public void LoadMetadata(string filename)
        {
            WriteLog("Loading metadata...");
            MetadataMarkers = Matrix.LoadMetadataMarkers(filename);
            WriteLog("Metadata loaded. Len:{0}", MetadataMarkers.Length.ToString());
        }
        public void SetRailsFromDictionary(Dictionary<Rails, float[]> dict)
        {
            V12Points = dict[Rails.V12];
            V5Points = dict[Rails.V5];
            V5SBPoints = dict[Rails.V5SB];
            V3_3Points = dict[Rails.V3_3];
        }

        public AtxStats Run()
        {
            OpenLog();

            int startingSign;
            AtxStats stats = new AtxStats();

            Parallel.For(0, 4, (i) =>
            {
                switch (i)
                {
                    case 0:
                        long[] peaks;
                        long[] edges;
                        // Search peaks and edges
                        WriteLog("Searching peaks+edges on V12...");
                        peaks = SearchPeaksEdges(V12Points, out startingSign, out edges, "V12");

                        stats.V12Stats.Points = V12Points;
                        stats.V12Stats.Peaks = peaks;
                        stats.V12Stats.Edges = edges;
                        stats.V12Stats.PeakStartingSign = startingSign;

                        WriteLog("Calculation complete for V12 Rail.");
                        break;
                    case 1:
                        WriteLog("Searching peaks+edges on V5...");
                        peaks = SearchPeaksEdges(V5Points, out startingSign, out edges, "V5");

                        stats.V5Stats.Points = V5Points;
                        stats.V5Stats.Peaks = peaks;
                        stats.V5Stats.Edges = edges;
                        stats.V5Stats.PeakStartingSign = startingSign;

                        WriteLog("Calculation complete for V5 Rail.");
                        break;
                    case 2:
                        WriteLog("Searching peaks+edges on V5SB...");
                        peaks = SearchPeaksEdges(V5SBPoints, out startingSign, out edges, "V5SB");

                        stats.V5SBStats.Points = V5SBPoints;
                        stats.V5SBStats.Peaks = peaks;
                        stats.V5SBStats.Edges = edges;
                        stats.V5SBStats.PeakStartingSign = startingSign;

                        WriteLog("Calculation complete for V5SB Rail.");
                        break;
                    case 3:
                        WriteLog("Searching peaks+edges on V3.3...");
                        peaks = SearchPeaksEdges(V3_3Points, out startingSign, out edges, "V3.3");

                        stats.V3_3Stats.Points = V3_3Points;
                        stats.V3_3Stats.Peaks = peaks;
                        stats.V3_3Stats.Edges = edges;
                        stats.V3_3Stats.PeakStartingSign = startingSign;

                        WriteLog("Calculation complete for V3.3 Rail.");
                        break;
                }
            });

            // Calculate full-rail stats
            WriteLog("Calculating full-rail stats...");
            Parallel.For(0, 4, (i) =>
            {
                switch (i)
                {
                    case 0:
                        CalcMeanMinMax(stats.V12Stats);
                        WriteLog("Calculation for rail V12 complete.");
                        break;
                    case 1:
                        CalcMeanMinMax(stats.V5Stats);
                        WriteLog("Calculation for rail V5 complete.");
                        break;
                    case 2:
                        CalcMeanMinMax(stats.V5SBStats);
                        WriteLog("Calculation for rail V5SB complete.");
                        break;
                    case 3:
                        CalcMeanMinMax(stats.V3_3Stats);
                        WriteLog("Calculation for rail V3.3 complete.");
                        break;
                }
            });

            WriteLog("Calculation complete for full-rail stats.");

            // Process metadata if available
            if (MetadataMarkers != null && MetadataMarkers.Length > 0)
            {
                WriteLog("Calculating rail segmented stats from metadata...");
                stats.SourceMetadata = MetadataMarkers;

                Parallel.For(0, 4, (i) =>
                {
                    WriteLog("Rail {0}/4 Pass 1/3 - full", (i+1).ToString());
                    float[] railData;
                    RailStats railStats;
                    switch (i)
                    {
                        case 0:
                            railStats = stats.V12Stats;
                            railData = V12Points;
                            break;
                        case 1:
                            railStats = stats.V5Stats;
                            railData = V5Points;
                            break;
                        case 2:
                            railStats = stats.V5SBStats;
                            railData = V5SBPoints;
                            break;
                        case 3:
                            railStats = stats.V3_3Stats;
                            railData = V3_3Points;
                            break;
                        default:
                            throw new InvalidOperationException(string.Format("[Rail {0} Pass --] Unexpected internal error. Rail data index invalid.", (i+1)));
                    }

                    WriteLog("Rail {0}/4 Pass 2/3 - inter", (i + 1).ToString());

                    // Calc inter-stage stats
                    for (int x = 1; x < MetadataLinesNames.Length; x++)
                    {
                        RailSegmentStats output = CalcSegmentStats(railData, x - 1, x, railStats, MetadataLinesNames);
                        if (output.MetadataIncomplete) WriteLog(string.Format("[Rail {0} Pass 2] Cannot perform analysis pass index={1}. Not enough metadata.", (i+1), x.ToString()));
                    }

                    WriteLog("Rail {0}/4 Pass 3/3 - activation", (i + 1).ToString());

                    // Calc stats from T1 to ON
                    RailSegmentStats onstats = CalcSegmentStats(railData, 0, 3, railStats, MetadataLinesNames);
                    if (onstats.MetadataIncomplete) WriteLog(string.Format("[Rail {0} Pass 3] Cannot perform analysis pass. Not enough metadata.", (i+1)));
                    stats.PgOkSignalTimeUs = onstats.DurationUs;
                });

                stats.LastStageRecorded = MetadataLinesNames[MetadataMarkers.Length - 1];
            } else
            {
                WriteLog("Warning: No metadata was loaded. Skipping rail segmented analysis");
            }

            CloseLog();

            return stats;
        }

        protected RailSegmentStats CalcSegmentStats(float[] railData, int start, int end, RailStats railStats,
            string[] MetadataLinesNames)
        {
            RailSegmentStats segment = new RailSegmentStats();
            segment.FromSignal = MetadataLinesNames[start];
            segment.ToSignal = MetadataLinesNames[end];
            long startFrame = start < MetadataMarkers.Length ? MetadataMarkers[start][0] : 0;
            long endFrame = end < MetadataMarkers.Length ? MetadataMarkers[end][0] : 0;
            if (startFrame < 0 || endFrame < 1 || endFrame < startFrame)
            {
                // Metadata incomplete
                segment.MetadataIncomplete = true;
                railStats.AppendSegment(segment);
                return segment;
            }

            float[] slice;
            slice = ExtractPointSegmentsFromMetadata(railData, MetadataMarkers, start, end);
            segment.Points = slice;
            segment.DurationUs = MetadataMarkers[end][1] - MetadataMarkers[start][1]; // Second index is time offset
            segment.DurationFrames = endFrame - startFrame; // First index is frame offset

            float mean;
            float min;
            float max;
            float deviation;

            CalcMeanMinMax(segment.Points, out mean, out min, out max, out deviation);

            segment.MeanVoltage = mean;
            segment.MaxVoltage = max;
            segment.MinVoltage = min;
            segment.Deviation = deviation;

            if (String.Equals(MetadataLinesNames[start], "ON", StringComparison.OrdinalIgnoreCase))
            {
                // Process ON/OFF regulation stats per rail
                long offPoints = 0;
                for (int y = 0; y < segment.Points.Length; y++)
                    if (Math.Abs(segment.Points[y] - railStats.NominalVoltage) > (railStats.NominalVoltage * railStats.VoltageTolerance)) offPoints++;

                OnStageRailStats onstats = new OnStageRailStats();

                onstats.OffRegulationPercent = (float)offPoints / segment.Points.Length;
                onstats.InRegulationPercent = (1 - onstats.OffRegulationPercent);
                onstats.DeviationVoltage = segment.Deviation;
                onstats.MeanVoltage = segment.MeanVoltage;

                railStats.OnStageStats = onstats;
            }

            if (String.Equals(MetadataLinesNames[start], "T2", StringComparison.OrdinalIgnoreCase))
            {
                // Process T2 regulation stats per rail
                double[] time = new double[segment.Points.Length];
                double[] value = new double[segment.Points.Length];

                for (int y = 0; y < value.Length; y++)
                {
                    time[y] = y;
                    value[y] = segment.Points[y];
                }

                RampUpStageRailStats t2stats = new RampUpStageRailStats();
                if (value.Length < 3)
                {
                    WriteLog(
                        "WARNING!: Insufficient data to generate a polynomial curve of the T2 stage. Not enough sampling rate on the board.");
                }
                else
                {
                    double[] coefficients = Fit.Polynomial(time, value, 2, DirectRegressionMethod.NormalEquations); // i, x1, x2

                    t2stats.CurveCoefficients = coefficients;
                    t2stats.Fitness = (float)GoodnessOfFit.RSquared(time.Select(x => coefficients[0] + (coefficients[1] * x) + (coefficients[2] * x * x)), value);
                }

                t2stats.TimeAxis = time;

                if (value.Length < 2)
                {
                    WriteLog(
                        "WARNING!: Insufficient data to generate a linear regression of the T2 stage. Not enough sampling rate on the board.");
                } else
                {
                    Tuple<double, double> line = Fit.Line(time, value);
                    t2stats.Slope = line.Item2;
                    t2stats.YIntercept = line.Item1;
                }

                railStats.T2StageStats = t2stats;
            }

            railStats.AppendSegment(segment);

            return segment;
        }

        public long[] SearchPeaksEdges(float[] points, out int startingSign, out long[] edges, string railName)
        {
            List<long> peaks = new List<long>();
            List<long> lstEdges = new List<long>();
            startingSign = POSITIVE;
            float windowSize = PeakDetectionWindowSizePercent * points.Length;

            // Inner loop vars
            float right;
            float left;
            float val;

            bool peak;
            bool valley;
            bool edgeLeft;
            bool edgeRight;
            bool different;

            int progress = 0;
            for (int i = 1; i < points.Length - 1; i++)
            {
                if (i - progress >= points.Length / 4)
                {
                    progress += points.Length / 4;
                    WriteLog("Progress for rail {0} is now {1}%...", railName, ((progress / (float)points.Length) * 100).ToString("N2"));
                }

                right = Math.Min(i, windowSize);
                left = Math.Min(points.Length - i - 1, windowSize);
                val = points[i];

                peak = true;
                valley = true;
                edgeLeft = false;
                edgeRight = false;
                different = false;

                if (i > 0 && Math.Abs(points[i - 1] - val) > EdgeDetectionSensitivity)
                    edgeLeft = true;

                if (i < points.Length - 1 && Math.Abs(points[i + 1] - val) > EdgeDetectionSensitivity)
                    edgeRight = true;



                // Search to the right
                int sign = 0;
                for (int r = 0; r < right; r++)
                {
                    int c = points[i - r - 1] > val ? POSITIVE : NEGATIVE;
                    if (sign == 0)
                        sign = c;
                    else
                    {
                        if (sign != c)
                            edgeRight = false;
                    }

                    if (Math.Abs(val - points[i - r - 1]) > EdgeDetectionSensitivity)
                        different = true;

                    if (points[i - r - 1] >= val)
                        peak = false;
                    if (points[i - r - 1] <= val)
                        valley = false;

                    if (!peak && !valley && !different) break;
                }

                // Search to the left
                sign = 0;
                for (int l = 0; l < left; l++)
                {
                    int c = points[i + l + 1] > val ? POSITIVE : NEGATIVE;
                    if (sign == 0)
                        sign = c;
                    else
                    {
                        if (sign != c)
                            edgeLeft = false;
                    }

                    if (Math.Abs(val - points[i + l + 1]) > EdgeDetectionSensitivity)
                        different = true;

                    if (points[i + l + 1] >= val)
                        peak = false;
                    if (points[i + l + 1] <= val)
                        valley = false;

                    if (!peak && !valley && !different) break;
                }

                if (peak || valley)
                {
                    // The current value is a local maxima or maxima
                    if (peaks.Count < 1)
                        startingSign = peak ? POSITIVE : NEGATIVE;
                    peaks.Add(i);
                    lstEdges.Add(i);
                    continue;
                }

                if ((edgeRight || edgeLeft) && different)
                {
                    // The current value is an edge
                    lstEdges.Add(i);
                    continue;
                }
            }

            edges = lstEdges.ToArray();

            WriteLog("[{0}] Found {1} peaks/valleys.", railName, peaks.Count.ToString());
            WriteLog("[{0}] First peak is: " + (startingSign == POSITIVE ? "POSITIVE" : "NEGATIVE"), railName);

            StringBuilder str = new StringBuilder(string.Format("[{0}] Peaks&Valleys: ", railName));
            for (int i = 0; i < peaks.Count; i++)
            {
                str.Append(peaks[i]);
                if (i < peaks.Count - 1)
                    str.Append(",");
            }
            WriteLog(str.ToString());

            WriteLog("[{0}] Found {1} edges.", railName, lstEdges.Count.ToString());

            str = new StringBuilder(string.Format("[{0}] Edges: ", railName));
            for (int i = 0; i < lstEdges.Count; i++)
            {
                str.Append(lstEdges[i]);
                if (i < lstEdges.Count - 1)
                    str.Append(",");
            }
            WriteLog(str.ToString());

            return peaks.ToArray();
        }

        protected void CalcMeanMinMax(RailStats rail)
        {
            float min;
            float max;
            float mean;
            float deviation; // Discarded

            CalcMeanMinMax(rail.Points, out mean, out min, out max, out deviation);

            rail.MeanVoltage = mean;
            rail.MinVoltage = min;
            rail.MaxVoltage = max;
        }

        protected void CalcMeanMinMax(float[] points, out float mean, out float min, out float max, out float deviation)
        {
            min = float.MaxValue;
            max = 0;
            mean = 0;
            deviation = 0;

            for (long i = 0; i < points.Length; i++)
            {
                float v = points[i];

                if (v > max)
                    max = v;
                if (v < min)
                    min = v;

                mean = mean + ((v - mean) / Math.Min((float)i + 1, 1000.0f));
            }

            // Calculate deviation
            for (long i = 0; i < points.Length; i++)
            {
                float v = points[i];
                v = Math.Abs(v - mean);

                deviation = deviation + ((v - deviation) / Math.Min((float)i + 1, 1000.0f));
            }
        }

        protected float[] ExtractPointSegments(float[] points, long from, long to)
        {
            float[] slice = new float[to - from];
            Array.Copy(points, from, slice, 0, slice.Length);

            return slice;
        }

        protected float[] ExtractPointSegmentsFromMetadata(float[] points, long[][] metadata, int fromMetadata,
            int toMetadata)
        {
            long start = MetadataMarkers[fromMetadata][0];
            long end = MetadataMarkers[toMetadata][0];

            if (end > points.Length)
            {
                WriteLog("Error!: Metadata is not aligned with the data stream. Forcing it...");
                end = points.Length;
                if (start >= end) start = end - 1;
            }

            return ExtractPointSegments(points, start, end);
        }

        protected virtual void OnLogEntryWritten(string message)
        {
            LogEntryWritten?.Invoke(this, message);
        }
    }
}
