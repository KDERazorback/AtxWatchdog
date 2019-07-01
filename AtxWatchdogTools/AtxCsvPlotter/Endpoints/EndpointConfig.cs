using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Runtime.Serialization;

namespace AtxCsvPlotter.Endpoints
{
    [DataContract]
    public class EndpointConfig
    {
        /// <summary>
        /// Stores the maximum value for the Vertical Axis, auto calculated from input data
        /// </summary>
        public float VAxisMax;

        /// <summary>
        /// Canvas where objects will be drawn onto
        /// </summary>
        public Bitmap Canvas;

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
        public Color LegendBackground { get; set; } = Color.Transparent;

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

        /// <summary>
        /// Specifies the path to an additional optional metadata file that contains additional information about the PSU transition states.
        /// </summary>
        public string MetadataFile { get; set; } = null;

        /// <summary>
        /// Stores the Colors that will be used to draw metadata lines on the plot. Must have exactly 4 Colors.
        /// </summary>
        public Color[] MetadataLinesColors { get; set; } =
        {
            Color.Aquamarine, // T1
            Color.BurlyWood, // T2
            Color.CornflowerBlue, // T3
            Color.GreenYellow, // PON
            Color.DarkSlateGray, // T6
        };

        /// <summary>
        /// Stores the names for each line that will be drawn on the plot from the Metadata file. Must have exactly 4 strings.
        /// </summary>
        public string[] MetadataLinesNames { get; set; }=
        {
            "T1",
            "T2",
            "T3",
            "ON",
            "T6",
            "OFF"
        };

        /// <summary>
        /// Stores the thickness of the lines drawn from Metadata onto the Plot. In in/100
        /// </summary>
        public int MetadataLinesThickness { get; set; } = 1;

        /// <summary>
        /// Stores the line style used to draw Metadata onto the Plot.
        /// </summary>
        public DashStyle MetadataLinesStyle { get; set; } = DashStyle.Dash;

        /// <summary>
        /// Specifies the font to use when drawing Metadata on the Plot
        /// </summary>
        public Font MetadataFont { get; set; } = new Font("Arial", 12.0f, GraphicsUnit.Point);
    }
}