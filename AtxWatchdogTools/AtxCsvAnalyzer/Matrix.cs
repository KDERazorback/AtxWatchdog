using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Set of utilities for loading and manipulating binary ATX data
    /// </summary>
    public static class Matrix
    {
        /// <summary>
        /// Loads rail data from an specified ASCII encoded CSV file. The resulting data is presented in a matrix with its axes swapped
        /// </summary>
        /// <param name="filename">Input ASCII encoded CSV filename</param>
        /// <param name="hasHeaders">A value indicating if the input data has headers, if not, a default order of t0,+12,+5,+5sb,+3.3 will be used</param>
        /// <param name="headers">Output data for the headers extracted from the CSV, or the defaul ones if the data did not contain any header information</param>
        /// <returns>A Y,X matrix populated with data from the CSV file</returns>
        public static float[][] LoadMatrix(string filename, bool hasHeaders, out string[] headers)
        {
            List<List<float>> matrix = new List<List<float>>();
            float maxValue = 0;
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (TextReader reader = new StreamReader(fs, Encoding.ASCII))
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        throw new IOException("The input file is empty.");

                    if (hasHeaders)
                    {
                        headers = line.Split(',');
                        line = reader.ReadLine();
                    }
                    else
                        headers = new string[] { "t0", "v12", "v5", "v5sb", "v3_3" };

                    int xcount = 0;
                    while (line != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            line = reader.ReadLine();
                            continue;
                        }

                        line = line.Trim();
                        if (line[0] == '#')
                        {
                            line = reader.ReadLine();
                            continue;
                        }

                        string[] cells = line.Split(',');
                        if (cells.Length <= 1)
                        {
                            line = reader.ReadLine();
                            continue;
                        }

                        while (matrix.Count < cells.Length)
                        {
                            List<float> vector = new List<float>(xcount);
                            while (vector.Count < xcount)
                                vector.Add(0);

                            matrix.Add(vector);
                        }

                        for (int i = 0; i < cells.Length; i++)
                        {
                            float v = float.Parse(cells[i]);
                            if (v > maxValue && i > 0)
                                maxValue = v;

                            matrix[i].Add(v);
                        }

                        xcount++;

                        line = reader.ReadLine();
                    }
                }
            }

            float[][] result = new float[matrix.Count][];

            for (int i = 0; i < result.Length; i++)
                result[i] = matrix[i].ToArray();

            return result;
        }

        /// <summary>
        /// Sorts the rail order of a loaded Data matrix, giving the least priority to the +5VSB rail and top priority to +3.3V rail
        /// </summary>
        /// <param name="matrix">Matrix of rails to be sorted</param>
        public static Dictionary<Rails, float[]> SortMatrix(Dictionary<Rails, float[]> matrix)
        {
            Dictionary<Rails, float[]> output = new Dictionary<Rails, float[]>();

            for (int i = 0; i < 4; i++)
            {
                Rails req = Rails.V12;
                switch (i)
                {
                    case 0:
                        req = Rails.V5SB;
                        break;
                    case 1:
                        req = Rails.V12;
                        break;
                    case 2:
                        req = Rails.V5;
                        break;
                    case 3:
                        req = Rails.V3_3;
                        break;
                }

                if (matrix.ContainsKey(req))
                    output.Add(req, matrix[req]);
            }

            return output;
        }

        /// <summary>
        /// Loads an specified metadata file and returns the markers present on it. A marker is an special tag that targets a single data point, where an special event occurred on the PSU.
        /// </summary>
        /// <param name="filename">File to load</param>
        /// <returns>A list of markers from the specified metadata file</returns>
        public static long[][] LoadMetadataMarkers(string filename)
        {
            List<long[]> markers = new List<long[]>();

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (fs.CanRead && fs.Position < fs.Length)
                {
                    long value = ReadLongFromStream(fs);

                    long time = ReadLongFromStream(fs);

                    markers.Add(new long[] { value, time });
                }
            }

            return markers.ToArray();
        }

        /// <summary>
        /// Reads a 32bit long data type in Big Endian encoding from the specified stream
        /// </summary>
        /// <param name="stream">Stream where the value will be read</param>
        /// <returns>A 32bit long value read from the specified stream</returns>
        public static long ReadLongFromStream(Stream stream)
        {
            int valh1 = stream.ReadByte();
            int vall1 = stream.ReadByte();
            int valh2 = stream.ReadByte();
            int vall2 = stream.ReadByte();

            long value = valh1;
            value <<= 8;
            value += vall1;

            value <<= 8;
            value += valh2;
            value <<= 8;
            value += vall2;

            return value;
        }
    }
}
