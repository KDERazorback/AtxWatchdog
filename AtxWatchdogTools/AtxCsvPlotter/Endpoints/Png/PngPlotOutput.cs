using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace AtxCsvPlotter.Endpoints.Png
{
    internal class PngPlotOutput : Endpoint
    {
        private bool _initialized = false;

        /// <summary>
        /// Graphics device used for drawing
        /// </summary>
        protected Graphics Device;

        public override string DefaultExtension => ".png";

        public override void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("This instance is already initialized");

            _initialized = true;

            Canvas = new Bitmap((int)InchesToPixels(PlotSize.Width), (int)InchesToPixels(PlotSize.Height));
            Device = Graphics.FromImage(Canvas);
        }

        public override void Plot(string inputFilename, string outputFilename, bool hasHeaders = true)
        {
            OutputFilename = outputFilename;

            Dictionary<Rails, float[]> series = new Dictionary<Rails, float[]>();

            float[][] data = LoadMatrix(inputFilename, hasHeaders, out string[] headers);
            for (int i = 0; i < headers.Length; i++)
            {
                Rails rail;
                if (Rails.TryParse(headers[i], true, out rail))
                    series.Add(rail, data[i]);
            }

            series = SortMatrix(series);

            // Clear graphics device
            Device.Clear(Background);
            __WriteTmpBitmap();

            // Draw grid
            // VAxis
            float stepValue = VAxisMax / VAxisSteps;

            Font rasterAxisFont = RasterFont(AxisLabelFont);
            Point lastLabelPos = Point.Empty;
            Size lastLabelSize = Size.Empty;
            for (int i = 0; i <= VAxisSteps; i++)
            {
                float y = stepValue * i;
                string ylabel;
                if (RoundVAxisSteps)
                {
                    y = (int) Math.Round(y, 0);
                    ylabel = y.ToString("N0");
                }
                else
                    ylabel = y.ToString("N2");

                y = PlotFunctionX(y);

                Device.DrawLine(new Pen(GridBrush, (float)InchesToPixels(GridThickness)),
                    PointToPixelSpace(new PointF(0, y)),
                    PointToPixelSpace(new PointF(PlotSpace.Width, y))
                    );

                // Draw label
                SizeF labelSize = Device.MeasureString(ylabel, rasterAxisFont);
                Point labelPos = PointToPixelSpace(new PointF(0 - AxisThickness, y));
                labelPos = new Point((int)(labelPos.X - labelSize.Width), (int)(labelPos.Y - (labelSize.Height / 2)));
                lastLabelPos = labelPos;
                Device.DrawString(ylabel, rasterAxisFont, AxisLabelBrush, labelPos);
                
                __WriteTmpBitmap();
            }

            if (DrawAxisName)
            {
                Font rasterFont = RasterFont(AxisNameFont);
                SizeF stringSize = Device.MeasureString(AxesNames[0], rasterFont);
                Bitmap textCanvas = new Bitmap((int)Math.Max(stringSize.Height, stringSize.Width),
                    (int)Math.Max(stringSize.Height, stringSize.Width));
                Graphics g = Graphics.FromImage(textCanvas);
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TranslateTransform(0, textCanvas.Height);
                g.RotateTransform(-90.0f);
                g.DrawString(AxesNames[0], rasterFont, AxisLabelBrush, Point.Empty);
                g.Dispose();

                Device.DrawImage(textCanvas,
                    new Rectangle((int)(lastLabelPos.X - lastLabelSize.Width - stringSize.Height), (int)InchesToPixels(PlotSpace.Height / 2.0f) + (int)stringSize.Width, (int)stringSize.Height, (int)stringSize.Width),
                    new Rectangle(0, 0, (int)stringSize.Height, textCanvas.Height),
                    GraphicsUnit.Pixel);

                textCanvas.Dispose();

                __WriteTmpBitmap();
            }

            // HAxis
            stepValue = (float)PlotSpace.Width / HAxisSteps;

            for (int i = 0; i <= HAxisSteps; i++)
            {
                int x = (int)(stepValue * i);
                string xlabel = x.ToString("N0");
                Device.DrawLine(new Pen(GridBrush, (float)InchesToPixels(GridThickness)),
                    PointToPixelSpace(new PointF(x, 0)),
                    PointToPixelSpace(new PointF(x, PlotSpace.Height))
                );

                // Draw label
                SizeF labelSize = Device.MeasureString(xlabel, rasterAxisFont);
                lastLabelSize = new Size((int)labelSize.Width, (int)labelSize.Height);
                Point labelPos = PointToPixelSpace(new PointF(x, 0 - AxisThickness));
                lastLabelPos = labelPos;
                labelPos = new Point((int) (labelPos.X - (labelSize.Width / 2)), (int)(labelPos.Y));
                Device.DrawString(xlabel, rasterAxisFont, AxisLabelBrush, labelPos);

                __WriteTmpBitmap();
            }

            if (DrawAxisName)
            {
                Font rasterFont = RasterFont(AxisNameFont);
                SizeF stringSize = Device.MeasureString(AxesNames[1], rasterFont);

                Point pos = PointToPixelSpace(new PointF(PlotSpace.Width / 2.0f, 0 - AxisThickness));
                pos = new Point((int)(pos.X - (stringSize.Width / 2.0f)), (int)(pos.Y + lastLabelSize.Height + stringSize.Height));

                Device.DrawString(AxesNames[1], rasterFont, AxisLabelBrush,
                    pos);

                __WriteTmpBitmap();
            }


            // Draw Axes
            Device.DrawLine(new Pen(AxisBrush, (int)InchesToPixels(AxisThickness)),
                PointToPixelSpace(new PointF(0, PlotSpace.Height)),
                PointToPixelSpace(new PointF(0, 0)));
            __WriteTmpBitmap();
            Device.DrawLine(new Pen(AxisBrush, (int)InchesToPixels(AxisThickness)),
                PointToPixelSpace(new PointF(0, 0)),
                PointToPixelSpace(new PointF(PlotSpace.Width, 0)));
            __WriteTmpBitmap();


            // Draw series
            stepValue = (float) PlotSpace.Width / series[0].Length;
            foreach (KeyValuePair<Rails, float[]> serie in series)
            {
                Color serieColor = Color.Black;
                switch (serie.Key)
                {
                    case Rails.V12:
                        serieColor = V12Color;
                        break;
                    case Rails.V5:
                        serieColor = V5Color;
                        break;
                    case Rails.V5SB:
                        serieColor = V5SBColor;
                        break;
                    case Rails.V3_3:
                        serieColor = V3_3Color;
                        break;
                }

                GraphicsPath path = new GraphicsPath(FillMode.Alternate);
                Point lastPoint = PointToPixelSpace(new PointF(0, PlotFunctionX(serie.Value[0])));
                for (int i = 1; i < serie.Value.Length; i++)
                {
                    Point point = PointToPixelSpace(new PointF(i * stepValue, PlotFunctionX(serie.Value[i])));
                    path.AddLine(lastPoint, point);
                    lastPoint = point;
                }

                if (ShowSeriesArea)
                {
                    Point p1 = PointToPixelSpace(new PointF(PlotSpace.Width, 0));
                    Point p2 = PointToPixelSpace(new PointF(0, 0));
                    path.AddLine(lastPoint, p1);
                    path.AddLine(p1, p2);
                    path.CloseFigure();

                    Device.FillPolygon(
                        new SolidBrush(Color.FromArgb((int) (SeriesAreaOpacity * 255), serieColor.R, serieColor.G,
                            serieColor.B)), path.PathPoints);
                    __WriteTmpBitmap();

                    Device.DrawPolygon(new Pen(serieColor, InchesToPixels(LineThickness)), path.PathPoints);
                    __WriteTmpBitmap();
                }
                else
                {
                    Device.DrawPath(new Pen(serieColor, InchesToPixels(LineThickness)), path);
                    __WriteTmpBitmap();
                }
            }

            // Draw legend
            if (DrawLegend)
            {
                Font rasterFont = RasterFont(LegendFont);
                float textHeight = Device.MeasureString("V", rasterFont).Height;
                float textTotalWidth = 0; // In pixels
                foreach (var serie in series)
                    textTotalWidth += Device.MeasureString(serie.Key.ToString(), rasterFont).Width;

                float span = InchesToPixels(LegendItemSeparation);
                textTotalWidth += (series.Count + 2) * span;
                textTotalWidth += series.Count * (LegendBulletSize.Width / 2.0f);

                Rectangle legendArea = new Rectangle(0, 0,
                    (int)(textTotalWidth + (series.Count * InchesToPixels(LegendBulletSize.Width))),
                    (int)(InchesToPixels((LegendVerticalPadding * 2) + LegendBulletSize.Height)));

                // Translate rectangle to the centroid
                legendArea = new Rectangle(InchesToPixels(LegendCentroid.X) - (legendArea.Width / 2),
                    InchesToPixels(LegendCentroid.Y) - (legendArea.Height / 2),
                    legendArea.Width, legendArea.Height);

                // Draw background
                Device.FillRectangle(new SolidBrush(LegendBackground), legendArea);

                // Draw border
                Device.DrawRectangle(new Pen(LegendBorderColor, InchesToPixels(LegendBorderThickness)), legendArea);

                __WriteTmpBitmap();

                // Draw series
                float vPos = (legendArea.Height / 2.0f) - (textHeight / 2.0f);
                float hPos = span;
                for (int i = 0; i < series.Count; i++)
                {
                    var serie = series.ElementAt(i);
                    Color serieColor = Color.Black;
                    switch (serie.Key)
                    {
                        case Rails.V12:
                            serieColor = V12Color;
                            break;
                        case Rails.V5:
                            serieColor = V5Color;
                            break;
                        case Rails.V5SB:
                            serieColor = V5SBColor;
                            break;
                        case Rails.V3_3:
                            serieColor = V3_3Color;
                            break;
                    }

                    // Draw bullet
                    Rectangle bulletRect = new Rectangle((int) hPos + legendArea.X,
                        (int) ((legendArea.Height / 2.0f) - InchesToPixels(LegendBulletSize.Height / 2.0f) + legendArea.Y),
                        (int) (InchesToPixels(LegendBulletSize.Width)),
                        (int) (InchesToPixels(LegendBulletSize.Height)));
                    Device.FillRectangle(new SolidBrush(serieColor),
                        bulletRect);

                    __WriteTmpBitmap();

                    SizeF writtenTextSize = Device.MeasureString(serie.Key.ToString(), rasterFont);
                    Device.DrawString(serie.Key.ToString(), rasterFont, AxisLabelBrush, new PointF(bulletRect.Right + bulletRect.Width, (int)vPos + legendArea.Y));

                    __WriteTmpBitmap();

                    hPos += span + (InchesToPixels(LegendBulletSize.Width) * 2) + writtenTextSize.Width;
                }
            }
        }

        public override void Finish()
        {
            Device.Dispose();
            Canvas.Save(OutputFilename, ImageFormat.Png);
        }
    }
}
