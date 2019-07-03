using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace AtxCsvPlotter.Endpoints
{
    [DataContract]
    public class EndpointConfig
    {
        // Auto-properties (non serializable)
        /// <summary>
        /// Gets the Pen used to draw gridlines on the Canvas
        /// </summary>
        public Pen GridPen => new Pen(GridColor, GridThickness) { DashStyle = GridDashStyle };

        /// <summary>
        /// Gets the Pen used to draw gridlines on the Canvas
        /// </summary>
        public Pen AxisPen => new Pen(AxisColor, AxisThickness) { DashStyle = AxisDashStyle };

        /// <summary>
        /// Gets the Brush used to draw axis labels on the Canvas
        /// </summary>
        public Brush AxisLabelBrush => new SolidBrush(AxisLabelColor);

        /// <summary>
        /// Gets the font used to draw metadata on the plot
        /// </summary>
        public Font MetadataFont =>
            new Font(MetadataFontFamily, MetadataFontSize, MetadataFontStyle, GraphicsUnit.Point);

        /// <summary>
        /// Gets the font used to draw axis values on the plot
        /// </summary>
        public Font AxisLabelFont =>
            new Font(AxisLabelFontFamily, AxisLabelFontSize, AxisLabelFontStyle, GraphicsUnit.Point);

        /// <summary>
        /// Gets the font used to draw the legend on the plot
        /// </summary>
        public Font LegendFont =>
            new Font(LegendFontFamily, LegendFontSize, LegendFontStyle, GraphicsUnit.Point);

        /// <summary>
        /// Gets the font used to draw the Axis name on the plot
        /// </summary>
        public Font AxisNameFont =>
            new Font(AxisNameFontFamily, AxisNameFontSize, AxisNameFontStyle, GraphicsUnit.Point);


        // Properties
        /// <summary>
        /// Stores the maximum value for the Vertical Axis, auto calculated from input data
        /// </summary>
        [DataMember] public float VAxisMax;

        /// <summary>
        /// Color used to draw the V12 rail
        /// </summary>
        [DataMember] public Color V12Color { get; set; } = Color.Yellow;

        /// <summary>
        /// Color used to draw the V5 rail
        /// </summary>
        [DataMember] public Color V5Color { get; set; } = Color.Red;

        /// <summary>
        /// Color used to draw the V3.3 rail
        /// </summary>
        [DataMember] public Color V3_3Color { get; set; } = Color.Orange;

        /// <summary>
        /// Color used to draw the V5SB rail
        /// </summary>
        [DataMember] public Color V5SBColor { get; set; } = Color.Magenta;

        /// <summary>
        /// Thickness for all Line series (in in/100)
        /// </summary>
        [DataMember] public int LineThickness { get; set; } = 4;

        /// <summary>
        /// Thickness for the grid lines (in in/100)
        /// </summary>
        [DataMember] public int GridThickness { get; set; } = 2;

        /// <summary>
        /// Stores the color of the grid lines drawn on the Canvas
        /// </summary>
        [DataMember] public Color GridColor { get; set; } = Color.Gray;

        /// <summary>
        /// Stores the line type that is used to draw the grid lines on the Canvas
        /// </summary>
        [DataMember] public DashStyle GridDashStyle { get; set; } = DashStyle.Solid;

        /// <summary>
        /// Dots per inch resolution for the output image
        /// </summary>
        [DataMember] public int Dpi { get; set; } = 150;

        /// <summary>
        /// Margins of the generated image, in in/100.
        /// </summary>
        [DataMember] public Margins Margins { get; set; } = new Margins(100, 100, 100, 100);

        /// <summary>
        /// Color used to draw the background of the plot canvas
        /// </summary>
        [DataMember] public Color Background { get; set; } = Color.White;

        /// <summary>
        /// Color used to draw each axis of the plot
        /// </summary>
        [DataMember] public Color AxisColor { get; set; } = Color.Black;

        /// <summary>
        /// Thickness for the axis lines (in in/100)
        /// </summary>
        [DataMember] public int AxisThickness { get; set; } = 6;

        /// <summary>
        /// Stores the line type that is used to draw the axis lines on the Canvas
        /// </summary>
        [DataMember] public DashStyle AxisDashStyle { get; set; } = DashStyle.Solid;

        /// <summary>
        /// Default size in in/100 for the plot space. Defaults to US-Letter paper size
        /// </summary>
        [DataMember] public Size PlotSize { get; set; } = new Size(1100, 850);

        /// <summary>
        /// Scale factor for the Vertical Axis Max value. The biggest value on the input data will be multiplied by this value to become the new axis max value.
        /// This will allow for a little free-space at the top of the plot.
        /// </summary>
        [DataMember] public float VerticalOverhead { get; set; } = 1.15f;

        /// <summary>
        /// Stores the amount of steps that will be drawn on the Vertical Axis. This value could change based on ratios between the plot area and the maximum data point
        /// </summary>
        [DataMember] public int VAxisSteps { get; set; } = 6;

        /// <summary>
        /// Stores the amount of steps that will be drawn on the Horizontal Axis. This value could change based on ratios between the plot area and the total amount of data points
        /// </summary>
        [DataMember] public int HAxisSteps { get; set; } = 14;

        /// <summary>
        /// If set, VAxis step lines will be rounded to the nearest whole data value. Note this will produce uneven grid lines on the VAxis
        /// </summary>
        [DataMember] public bool RoundVAxisSteps { get; set; } = true;

        /// <summary>
        /// Specifies the font family used to draw Axis values on the Canvas
        /// </summary>
        [DataMember] public string AxisLabelFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Specifies the font style to use when drawing Axis values on the Plot
        /// </summary>
        [DataMember] public FontStyle AxisLabelFontStyle { get; set; } = FontStyle.Regular;

        /// <summary>
        /// Specifies the font size in Points to use when drawing Axis values on the Plot
        /// </summary>
        [DataMember] public float AxisLabelFontSize { get; set; } = 26.0f;

        /// <summary>
        /// Specifies the color used to draw axes labels
        /// </summary>
        [DataMember] public Color AxisLabelColor { get; set; } = Color.Black;

        /// <summary>
        /// Indicates if Series should be closed as Polygons, with its interior filled
        /// </summary>
        [DataMember] public bool ShowSeriesArea { get; set; } = true;

        /// <summary>
        /// Indicates the opacity of the filled area of closed Series. This value only has effect if <see cref="ShowSeriesArea"/> is set
        /// </summary>
        [DataMember] public float SeriesAreaOpacity { get; set; } = 0.15f;

        /// <summary>
        /// Specifies if the Endpoint should plot the chart legend too
        /// </summary>
        [DataMember] public bool DrawLegend { get; set; } = true;

        /// <summary>
        /// Specifies the Font family to use when drawing the Series Legend on the plot
        /// </summary>
        [DataMember] public string LegendFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Specifies the font style to use when drawing the Legend on the Plot
        /// </summary>
        [DataMember] public FontStyle LegendFontStyle { get; set; } = FontStyle.Regular;

        /// <summary>
        /// Specifies the font size in Points to use when drawing the Legend on the Plot
        /// </summary>
        [DataMember] public float LegendFontSize { get; set; } = 18.0f;

        /// <summary>
        /// Specifies the center of the Series Legend when drawn onto the Plot, in in/100
        /// </summary>
        [DataMember] public PointF LegendCentroid { get; set; } = new PointF(550, 50);

        /// <summary>
        /// Specifies the bullet size used to draw the Series legend on the Canvas, in in/100
        /// </summary>
        [DataMember] public Size LegendBulletSize { get; set; } = new Size(8, 8);

        /// <summary>
        /// Specifies the color used to draw the background of the Legend
        /// </summary>
        [DataMember] public Color LegendBackground { get; set; } = Color.Transparent;

        /// <summary>
        /// Specifies the color used to draw the border on the Legend
        /// </summary>
        [DataMember] public Color LegendBorderColor { get; set; } = Color.Black;

        /// <summary>
        /// Specifies the thickness of the Legend's border, in in/100
        /// </summary>
        [DataMember] public int LegendBorderThickness { get; set; } = 2;

        /// <summary>
        /// Specifies the separation between items on the Plot Legend, in in/100
        /// </summary>
        [DataMember] public int LegendItemSeparation { get; set; } = 22;

        /// <summary>
        /// Specifies the padding added to the Top and Bottom portions of the Legend, between the border and the contents.
        /// In in/100
        /// </summary>
        [DataMember] public int LegendVerticalPadding { get; set; } = 12;

        /// <summary>
        /// Indicates if the Endpoint should draw Axis labels on the plot
        /// </summary>
        [DataMember] public bool DrawAxisName { get; set; } = true;

        /// <summary>
        /// Specifies the names for the Plot Axes, starting with the Y axis
        /// </summary>
        [DataMember] public string[] AxesNames { get; set; } = new string[] { "VOLTS", "TIME" };

        /// <summary>
        /// Specifies the font to use when drawing an Axis name onto the plot
        /// </summary>
        [DataMember] public string AxisNameFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Specifies the font style to use when drawing the Axis name on the Plot
        /// </summary>
        [DataMember] public FontStyle AxisNameFontStyle { get; set; } = FontStyle.Regular;

        /// <summary>
        /// Specifies the font size in Points to use when drawing the Axis name on the Plot
        /// </summary>
        [DataMember] public float AxisNameFontSize { get; set; } = 18.0f;

        /// <summary>
        /// Specifies the path to an additional optional metadata file that contains additional information about the PSU transition states.
        /// </summary>
        [DataMember] public string MetadataFile { get; set; } = null;

        /// <summary>
        /// Stores the Colors that will be used to draw metadata lines on the plot. Must have exactly 4 Colors.
        /// </summary>
        [DataMember]
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
        [DataMember]
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
        [DataMember] public int MetadataLinesThickness { get; set; } = 1;

        /// <summary>
        /// Stores the line style used to draw Metadata onto the Plot.
        /// </summary>
        [DataMember] public DashStyle MetadataLinesStyle { get; set; } = DashStyle.Dash;

        /// <summary>
        /// Specifies the font family to use when drawing Metadata on the Plot
        /// </summary>
        [DataMember] public string MetadataFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Specifies the font style to use when drawing Metadata on the Plot
        /// </summary>
        [DataMember] public FontStyle MetadataFontStyle { get; set; } = FontStyle.Regular;

        /// <summary>
        /// Specifies the font size in Points to use when drawing Metadata on the Plot
        /// </summary>
        [DataMember] public float MetadataFontSize { get; set; } = 12.0f;

        /// <summary>
        /// Indicates if the plot should contain timestamps from the markers extracted from the metadata
        /// </summary>
        [DataMember] public bool DrawTimestampsOnMarkers { get; set; } = false;

        // Serialization methods
        public void SaveTo(string filename)
        {
            SaveTo(filename, this);
        }

        // Static config serializers
        public static void SaveTo(string filename, EndpointConfig instance)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                DataContractSerializer ser = new DataContractSerializer(typeof(EndpointConfig));
                XmlWriterSettings settings = new XmlWriterSettings { Indent = true };
                XmlWriter write = XmlWriter.Create(fs, settings);

                ser.WriteObject(write, instance);

                write.Flush();
                write.Dispose();
            }
        }

        public static EndpointConfig Loadfrom(string filename)
        {
            EndpointConfig output;
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                DataContractSerializer ser = new DataContractSerializer(typeof(EndpointConfig));
                output = (EndpointConfig) ser.ReadObject(fs);
            }

            return output;
        }
    }
}