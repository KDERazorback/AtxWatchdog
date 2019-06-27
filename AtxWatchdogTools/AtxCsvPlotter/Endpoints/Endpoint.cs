using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace AtxCsvPlotter.Endpoints
{
    internal abstract class Endpoint
    {
        // Implemented variables
        protected string OutputFilename;

        // Implemented properties
        /// <summary>
        /// Color used to draw the V12 rail
        /// </summary>
        public Color V12Color { get; set; } = Color.Yellow;
        /// <summary>
        /// Color used to draw the V5 rail
        /// </summary>
        public Color V5Color { get; set; } = Color.Red;
        /// <summary>
        /// Color used to draw the V3.3 rail
        /// </summary>
        public Color V3_3Color { get; set; } = Color.Orange;
        /// <summary>
        /// Color used to draw the V5SB rail
        /// </summary>
        public Color V5SBColor { get; set; } = Color.Magenta;

        /// <summary>
        /// Thickness for all Line series (in in/100)
        /// </summary>
        public int LineThickness { get; set; } = 4;

        /// <summary>
        /// Brush used to draw gridlines on the Canvas
        /// </summary>
        public Brush GridBrush { get; set; } = new SolidBrush(Color.Gray);

        /// <summary>
        /// Thickness for the grid lines (in in/100)
        /// </summary>
        public int GridThickness { get; set; } = 2;

        /// <summary>
        /// Dots per inch resolution for the output image
        /// </summary>
        public int Dpi { get; set; } = 150;

        /// <summary>
        /// Margins of the generated image, in in/100.
        /// </summary>
        public Margins Margins { get; set; } = new Margins(100, 100, 100, 100);

        /// <summary>
        /// Color used to draw the background of the plot canvas
        /// </summary>
        public Color Background { get; set; } = Color.White;

        /// <summary>
        /// Brush used to draw each axis of the plot
        /// </summary>
        public Brush AxisBrush { get; set; } = new SolidBrush(Color.Black);
        /// <summary>
        /// Thickness for the axis lines (in in/100)
        /// </summary>
        public int AxisThickness { get; set; } = 6;

        /// <summary>
        /// Default size in in/100 for the plot space. Defaults to US-Letter paper size
        /// </summary>
        public Size PlotSize { get; set; } = new Size(1100, 850);

        /// <summary>
        /// Scale factor for the Vertical Axis Max value. The biggest value on the input data will be multiplied by this value to become the new axis max value.
        /// This will allow for a little free-space at the top of the plot.
        /// </summary>
        public float VerticalOverhead { get; set; } = 1.15f;

        /// <summary>
        /// Stores the amount of steps that will be drawn on the Vertical Axis. This value could change based on ratios between the plot area and the maximum data point
        /// </summary>
        public int VAxisSteps { get; set; } = 6;

        /// <summary>
        /// Stores the amount of steps that will be drawn on the Horizontal Axis. This value could change based on ratios between the plot area and the total amount of data points
        /// </summary>
        public int HAxisSteps { get; set; } = 14;

        /// <summary>
        /// If set, VAxis step lines will be rounded to the nearest whole data value. Note this will produce uneven grid lines on the VAxis
        /// </summary>
        public bool RoundVAxisSteps { get; set; } = true;

        /// <summary>
        /// Specifies the font used to draw Axis text on the Canvas in inches
        /// </summary>
        public Font AxisLabelFont { get; set; } = new Font("Arial", 26.0f, GraphicsUnit.Point);

        /// <summary>
        /// Specifies the brush used to draw axes labels
        /// </summary>
        public Brush AxisLabelBrush { get; set; } = new SolidBrush(Color.Black);

        /// <summary>
        /// Indicates if Series should be closed as Polygons, with its interior filled
        /// </summary>
        public bool ShowSeriesArea { get; set; } = true;

        /// <summary>
        /// Indicates the opacity of the filled area of closed Series. This value only has effect if <see cref="ShowSeriesArea"/> is set
        /// </summary>
        public float SeriesAreaOpacity { get; set; } = 0.15f;
        /// <summary>
        /// Stores the maximum value for the Vertical Axis, auto calculated from input data
        /// </summary>
        protected float VAxisMax;
        /// <summary>
        /// Canvas where objects will be drawn onto
        /// </summary>
        protected Bitmap Canvas;

        /// <summary>
        /// Whole picture area, in in/100
        /// </summary>
        protected Rectangle Space => new Rectangle(Point.Empty, PlotSize);
        /// <summary>
        /// Area for the plot in in/100. Excluding its margins
        /// </summary>
        protected Rectangle PlotSpace
        {
            get
            {
                return new Rectangle(Margins.Left, Margins.Top, PlotSize.Width - Margins.Left - Margins.Right,
                    PlotSize.Height - Margins.Top - Margins.Bottom);
            }
        }

        /// <summary>
        /// Specifies if the Endpoint should plot the chart legend too
        /// </summary>
        public bool DrawLegend { get; set; } = true;

        /// <summary>
        /// Specifies the Font to use when drawing the Series Legend
        /// </summary>
        public Font LegendFont { get; set; } = new Font("Arial", 18.0f, GraphicsUnit.Point);

        /// <summary>
        /// Specifies the center of the Series Legend when drawn onto the Plot, in in/100
        /// </summary>
        public PointF LegendCentroid { get; set; } = new PointF(550, 50);

        /// <summary>
        /// Specifies the bullet size used to draw the Series legend on the Canvas, in in/100
        /// </summary>
        public Size LegendBulletSize { get; set; } = new Size(8, 8);

        /// <summary>
        /// Specifies the color used to draw the background of the Legend
        /// </summary>
        public Color LegendBackground { get; set; } = Color.White;

        /// <summary>
        /// Specifies the color used to draw the border on the Legend
        /// </summary>
        public Color LegendBorderColor { get; set; } = Color.Black;

        /// <summary>
        /// Specifies the thickness of the Legend's border, in in/100
        /// </summary>
        public int LegendBorderThickness { get; set; } = 2;

        /// <summary>
        /// Specifies the separation between items on the Plot Legend, in in/100
        /// </summary>
        public int LegendItemSeparation { get; set; } = 22;

        /// <summary>
        /// Specifies the padding added to the Top and Bottom portions of the Legend, between the border and the contents.
        /// In in/100
        /// </summary>
        public int LegendVerticalPadding { get; set; } = 12;

        /// <summary>
        /// Indicates if the Endpoint should draw Axis labels on the plot
        /// </summary>
        public bool DrawAxisName { get; set; } = true;

        /// <summary>
        /// Specifies the names for the Plot Axes, starting with the Y axis
        /// </summary>
        public string[] AxesNames { get; set; } = new string[] { "VOLTS", "TIME" };

        /// <summary>
        /// Specifies the font to use when drawing an Axis name onto the plot
        /// </summary>
        public Font AxisNameFont { get; set; } = new Font("Arial", 18.0f, GraphicsUnit.Point);
        

        // Required properties
        /// <summary>
        /// Default extension for the output files
        /// </summary>
        public abstract string DefaultExtension { get; }

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
            return (int)Math.Max(1, Math.Round(cinch * (Dpi / 100.0f)));
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
            return (x_value * PlotSpace.Height) / VAxisMax;
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

        private int _tmpBitmapIndex = 0;
        protected void __WriteTmpBitmap()
        {
            // DISABLED //
            /*
            FileInfo fi = new FileInfo(OutputFilename);
            string tmpFilename = fi.FullName.Substring(0, fi.FullName.Length - fi.Extension.Length) + "_" +
                                 _tmpBitmapIndex + DefaultExtension;
            Canvas.Save(tmpFilename, ImageFormat.Png);
            _tmpBitmapIndex++;
            */
        }

        /// <summary>
        /// Loads rail data from an specified ASCII encoded CSV file. The resulting data is presented in a matrix with its axes swapped
        /// </summary>
        /// <param name="filename">Input ASCII encoded CSV filename</param>
        /// <param name="hasHeaders">A value indicating if the input data has headers, if not, a default order of +12,+5,+5sb,+3.3 will be used</param>
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
                        headers = new string[] { "v12", "v5", "v5sb", "v3_3" };

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
                            if (v > maxValue)
                                maxValue = v;

                            matrix[i].Add(v);
                        }

                        xcount++;

                        line = reader.ReadLine();
                    }
                }
            }

            // Calculate and store VAxis limit
            VAxisMax = maxValue * VerticalOverhead;

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
    }
}
