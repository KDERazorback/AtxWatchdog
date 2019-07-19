using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace AtxCsvPlotter.Endpoints
{
    internal abstract class Endpoint
    {
        // Implemented variables
        protected string OutputFilename;

        // Implemented properties

        /// <summary>
        /// Whole picture area, in in/100
        /// </summary>
        protected Rectangle Space => new Rectangle(Point.Empty, Config.PlotSize);
        /// <summary>
        /// Area for the plot in in/100. Excluding its margins
        /// </summary>
        protected Rectangle PlotSpace
        {
            get
            {
                return new Rectangle(Config.Margins.Left, Config.Margins.Top, Config.PlotSize.Width - Config.Margins.Left - Config.Margins.Right, Config.PlotSize.Height - Config.Margins.Top - Config.Margins.Bottom);
            }
        }

        // Required properties
        /// <summary>
        /// Default extension for the output files
        /// </summary>
        public abstract string DefaultExtension { get; }

        /// <summary>
        ///  Stores the configuration used to generate the plot
        /// </summary>
        public EndpointConfig Config { get; set; } = new EndpointConfig();

        // Required methods
        public abstract void Initialize();
        public abstract void Plot(string inputFilename, string outputFilename, bool hasHeaders = true);
        public abstract void Finish();

        // Helper Methods
        /// <summary>
        /// Converts a length value in in/100 to its equivalent in pixels using the current <see cref="Dpi"/> setting
        /// </summary>
        /// <param name="cinch">Length value in in/100</param>
        /// <returns>Amount of pixels that represent the specified length</returns>
        protected int InchesToPixels(float cinch)
        {
            return (int)Math.Max(1, Math.Round(cinch * (Config.Dpi / 100.0f)));
        }

        /// <summary>
        /// Converts a Rectangle object in in/100 to its equivalent in pixels using the current <see cref="Dpi"/> setting
        /// </summary>
        /// <param name="rectInches">Rectangle object in in/100</param>
        /// <returns>An equivalent Rectangle that uses pixels on all its dimensions</returns>
        protected Rectangle InchesToPixels(Rectangle rectInches)
        {
            return new Rectangle(InchesToPixels(rectInches.Location), InchesToPixels(rectInches.Size));
        }

        /// <summary>
        /// Converts a point value in in/100 to its equivalent in pixels using the current <see cref="Dpi"/> setting
        /// </summary>
        /// <param name="pointInches">Point value in in/100</param>
        /// <returns>An equivalent Point that uses pixels on all its coordinates</returns>
        protected Point InchesToPixels(Point pointInches)
        {
            return new Point((int)InchesToPixels(pointInches.X), (int)InchesToPixels(pointInches.Y));
        }

        /// <summary>
        /// Converts a Size value in in/100 to its equivalent in pixels using the current <see cref="Dpi"/> setting
        /// </summary>
        /// <param name="sizeInches">Size value in in/100</param>
        /// <returns>An equivalent Size that uses pixels on all its axes</returns>
        protected Size InchesToPixels(Size sizeInches)
        {
            return new Size((int)InchesToPixels(sizeInches.Width), (int)InchesToPixels(sizeInches.Height));
        }

        /// <summary>
        /// Converts a length value in in/100 to its equivalent in pixels using the current <see cref="Dpi"/> setting
        /// </summary>
        /// <param name="cinch">Length value in in/100</param>
        /// <returns>Amount of pixels that represent the specified length</returns>
        protected float InchesToPixels(int cinch)
        {
            return InchesToPixels((float)cinch);
        }

        /// <summary>
        /// Converts a Point from local Plot coordinates to Canvas space in in/100
        /// </summary>
        /// <param name="p">Point in Plot space to convert</param>
        /// <returns>A Point in Canvas space</returns>
        protected PointF PointToSpace(PointF p)
        {
            return new PointF(p.X + PlotSpace.X, p.Y + PlotSpace.Y);
        }

        /// <summary>
        /// Converts a Point from local Plot coordinates to Pixel space Canvas coordinates. This also inverts the Y axis orientation of the coordinate.
        /// </summary>
        /// <param name="p">Point in Plot space to convert</param>
        /// <returns>A Point in Pixel space</returns>
        protected Point PointToPixelSpace(PointF p)
        {
            return new Point(InchesToPixels(p.X + PlotSpace.X), InchesToPixels((PlotSpace.Height - p.Y) + PlotSpace.Y));
        }

        /// <summary>
        /// Converts a Size from local Plot coordinates to Pixel space Canvas coordinates
        /// </summary>
        /// <param name="sz">Size in Plot space to convert</param>
        /// <returns>A Size in Pixel space</returns>
        protected Size SizeToPixelSpace(SizeF sz)
        {
            return new Size(InchesToPixels(sz.Width + PlotSpace.X), InchesToPixels(sz.Height + PlotSpace.Y));
        }

        /// <summary>
        /// Converts a Size from local Plot coordinates to Canvas space in in/100
        /// </summary>
        /// <param name="sz">Size in Plot space to convert</param>
        /// <returns>A Size in Canvas space</returns>
        protected SizeF SizeToSpace(SizeF sz)
        {
            return new SizeF(sz.Width + PlotSpace.X, sz.Height + PlotSpace.Y);
        }

        /// <summary>
        /// Performs the F(x) function calculation, returning the real Y coordinate in Plot Space from a given data input
        /// </summary>
        /// <param name="x_value">Input value from the Data matrix</param>
        /// <returns>Y coordinate for the input value, in Plot Space</returns>
        protected float PlotFunctionX(float x_value)
        {
            return (x_value * PlotSpace.Height) / Config.VAxisMax;
        }

        /// <summary>
        /// Converts a font using a Point GraphicsUnit to one using a Pixel GraphicsUnit using the specified DPI config
        /// </summary>
        /// <param name="f">Font to be converted</param>
        /// <returns>A converted font that uses Pixels as its GraphicsUnit</returns>
        protected Font RasterFont(Font f)
        {
            return new Font(f.FontFamily, InchesToPixels((f.SizeInPoints * 72) / 100), f.Style, GraphicsUnit.Pixel,
                f.GdiCharSet, f.GdiVerticalFont);
        }

        /// <summary>
        /// Converts a pen using Point units into one using Pixel units, based on the specified DPI config
        /// </summary>
        /// <param name="p">Pen to be converted</param>
        /// <returns>A converted pen that uses Pixels as its units</returns>
        protected Pen RasterPen(Pen p)
        {
            return new Pen(p.Color, p.Width)
            {
                Alignment = p.Alignment,
                //CompoundArray = p.CompoundArray,
                //CustomStartCap = p.CustomStartCap,
                //CustomEndCap = p.CustomEndCap,
                //DashCap = p.DashCap,
                DashStyle = p.DashStyle,
                DashOffset = p.DashOffset,
                //DashPattern = p.DashPattern,
                //StartCap = p.StartCap,
                //EndCap = p.EndCap,
                Transform = p.Transform,
                //LineJoin = p.LineJoin,
                //MiterLimit = p.MiterLimit
            };
        }

        /// <summary>
        /// Loads rail data from an specified ASCII encoded CSV file. The resulting data is presented in a matrix with its axes swapped
        /// </summary>
        /// <param name="filename">Input ASCII encoded CSV filename</param>
        /// <param name="hasHeaders">A value indicating if the input data has headers, if not, a default order of t0,+12,+5,+5sb,+3.3 will be used</param>
        /// <param name="headers">Output data for the headers extracted from the CSV, or the defaul ones if the data did not contain any header information</param>
        /// <returns>A Y,X matrix populated with data from the CSV file</returns>
        protected float[][] LoadMatrix(string filename, bool hasHeaders, out string[] headers)
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

            // Calculate and store VAxis limit
            Config.VAxisMax = maxValue * Config.VerticalOverhead;

            float[][] result = new float[matrix.Count][];

            for (int i = 0; i < result.Length; i++)
                result[i] = matrix[i].ToArray();

            return result;
        }

        /// <summary>
        /// Sorts the rail order of a loaded Data matrix, giving the least priority to the +5VSB rail and top priority to +3.3V rail
        /// </summary>
        /// <param name="matrix">Matrix of rails to be sorted</param>
        protected Dictionary<Rails, float[]> SortMatrix(Dictionary<Rails, float[]> matrix)
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
        protected long[][] LoadMetadataMarkers(string filename)
        {
            List<long[]> markers = new List<long[]>();

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (fs.CanRead && fs.Position < fs.Length)
                {
                    long value = ReadLongFromStream(fs);

                    if (value < 1)
                        break;

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
        protected virtual long ReadLongFromStream(Stream stream)
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
