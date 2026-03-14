using System.Drawing.Drawing2D;
using System.Globalization;

namespace FortniteReplayAnalyzer;

internal sealed class GraphExplorerPoint
{
    public required string Label { get; init; }
    public required double XValue { get; init; }
    public required double Value { get; init; }
}

internal sealed class GraphExplorerSeries
{
    public required string Name { get; init; }
    public required List<GraphExplorerPoint> Points { get; init; }
    public required Color Color { get; init; }
}

internal enum GraphExplorerMode
{
    Line,
    Bar
}

internal sealed class GraphExplorerForm : Form
{
    private readonly Panel _canvas;
    private readonly ToolTip _toolTip = new();
    private readonly CheckBox _chkLogarithmic;
    private readonly List<GraphExplorerSeries> _series;
    private readonly GraphExplorerMode _mode;
    private readonly string _xAxisTitle;
    private readonly string _yAxisTitle;
    private RectangleF _plotBounds;
    private double _minX;
    private double _maxX;
    private double _minY;
    private double _maxY;
    private bool _isPanning;
    private Point _lastMousePoint;

    private GraphExplorerForm(
        string title,
        GraphExplorerMode mode,
        List<GraphExplorerSeries> series,
        string xAxisTitle,
        string yAxisTitle)
    {
        Text = title;
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 500);

        _mode = mode;
        _series = series;
        _xAxisTitle = xAxisTitle;
        _yAxisTitle = yAxisTitle;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(8, 6, 8, 6),
            WrapContents = false
        };

        _chkLogarithmic = new CheckBox
        {
            AutoSize = true,
            Text = "Logarithmic Y Axis"
        };
        _chkLogarithmic.CheckedChanged += (_, _) => _canvas.Invalidate();

        var btnResetZoom = new Button
        {
            AutoSize = true,
            Text = "Reset View"
        };
        btnResetZoom.Click += (_, _) =>
        {
            ResetView();
            _canvas.Invalidate();
        };

        toolbar.Controls.Add(_chkLogarithmic);
        toolbar.Controls.Add(btnResetZoom);

        _canvas = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _canvas.Paint += (_, e) => PaintCanvas(e.Graphics, _canvas.ClientRectangle);
        _canvas.MouseMove += (_, e) => HandleMouseMove(e.Location);
        _canvas.MouseLeave += (_, _) => _toolTip.SetToolTip(_canvas, string.Empty);
        _canvas.MouseWheel += (_, e) => HandleMouseWheel(e);
        _canvas.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !_plotBounds.Contains(e.Location))
            {
                return;
            }

            _isPanning = true;
            _lastMousePoint = e.Location;
            _canvas.Cursor = Cursors.SizeWE;
        };
        _canvas.MouseUp += (_, _) =>
        {
            _isPanning = false;
            _canvas.Cursor = Cursors.Default;
        };

        Controls.Add(_canvas);
        Controls.Add(toolbar);

        ResetView();
    }

    internal static GraphExplorerForm CreateTimeline(string title, List<(string Name, List<DamageTimelinePoint> Points)> seriesRows, bool cumulative)
    {
        var palette = new[]
        {
            Color.FromArgb(25, 118, 210),
            Color.FromArgb(0, 150, 136),
            Color.FromArgb(244, 81, 30),
            Color.FromArgb(123, 31, 162),
            Color.FromArgb(255, 179, 0)
        };

        var series = seriesRows.Select((row, index) => new GraphExplorerSeries
        {
            Name = row.Name,
            Color = palette[index % palette.Length],
            Points = row.Points
                .Select(point => new GraphExplorerPoint
                {
                    Label = FormatMatchClock(point.TimeValue),
                    XValue = point.TimeValue,
                    Value = point.Damage
                })
                .ToList()
        }).ToList();

        return new GraphExplorerForm(
            title,
            cumulative ? GraphExplorerMode.Line : GraphExplorerMode.Bar,
            series,
            "Match Time",
            "Damage");
    }

    internal static GraphExplorerForm CreateBar(string title, List<GraphExplorerPoint> points, Color color)
    {
        return new GraphExplorerForm(
            title,
            GraphExplorerMode.Bar,
            [
                new GraphExplorerSeries
                {
                    Name = title,
                    Color = color,
                    Points = points.Select((point, index) => new GraphExplorerPoint
                    {
                        Label = point.Label,
                        XValue = index,
                        Value = point.Value
                    }).ToList()
                }
            ],
            "Replay",
            "Value");
    }

    private void ResetView()
    {
        var allPoints = _series.SelectMany(series => series.Points).ToList();
        _minX = 0D;
        _maxX = Math.Max(1D, allPoints.Count == 0 ? 1D : allPoints.Max(point => point.XValue));
        _minY = 0D;
        _maxY = Math.Max(1D, allPoints.Select(point => point.Value).DefaultIfEmpty(1D).Max());
    }

    private void HandleMouseWheel(MouseEventArgs e)
    {
        if (!_plotBounds.Contains(e.Location))
        {
            return;
        }

        var factor = e.Delta > 0 ? 0.8 : 1.25;
        if (_mode == GraphExplorerMode.Bar)
        {
            var centerX = PixelToDomainX(e.Location.X);
            var halfRange = (_maxX - _minX) * factor / 2D;
            _minX = Math.Max(0D, centerX - halfRange);
            _maxX = Math.Max(_minX + 1D, centerX + halfRange);
        }
        else
        {
            var centerX = PixelToDomainX(e.Location.X);
            var centerY = PixelToDomainY(e.Location.Y);
            var halfRangeX = (_maxX - _minX) * factor / 2D;
            var halfRangeY = (_maxY - _minY) * factor / 2D;
            _minX = Math.Max(0D, centerX - halfRangeX);
            _maxX = Math.Max(_minX + 1D, centerX + halfRangeX);
            _minY = Math.Max(0D, centerY - halfRangeY);
            _maxY = Math.Max(_minY + 1D, centerY + halfRangeY);
        }

        _canvas.Invalidate();
    }

    private void HandleMouseMove(Point location)
    {
        if (_isPanning && _plotBounds.Contains(location))
        {
            var deltaX = PixelToDomainX(_lastMousePoint.X) - PixelToDomainX(location.X);
            _minX = Math.Max(0D, _minX + deltaX);
            _maxX = Math.Max(_minX + 1D, _maxX + deltaX);
            _lastMousePoint = location;
            _canvas.Invalidate();
            return;
        }

        if (!_plotBounds.Contains(location))
        {
            _toolTip.SetToolTip(_canvas, string.Empty);
            return;
        }

        var tooltip = _mode == GraphExplorerMode.Line
            ? BuildLineTooltip(location)
            : BuildBarTooltip(location);
        _toolTip.SetToolTip(_canvas, tooltip);
    }

    private string BuildLineTooltip(Point location)
    {
        var xValue = PixelToDomainX(location.X);
        var lines = new List<string> { FormatMatchClock(xValue) };

        foreach (var series in _series)
        {
            var points = series.Points;
            if (points.Count == 0)
            {
                continue;
            }

            if (xValue <= points[0].XValue)
            {
                lines.Add($"{series.Name}: {points[0].Value:0.#}");
                continue;
            }

            var lastPoint = points[^1];
            if (xValue >= lastPoint.XValue)
            {
                lines.Add($"{series.Name}: {lastPoint.Value:0.#}");
                continue;
            }

            GraphExplorerPoint lowerPoint = points[0];
            GraphExplorerPoint upperPoint = points[^1];
            for (var index = 1; index < points.Count; index++)
            {
                if (points[index].XValue < xValue)
                {
                    lowerPoint = points[index];
                    continue;
                }

                upperPoint = points[index];
                break;
            }

            var xRange = upperPoint.XValue - lowerPoint.XValue;
            var ratio = xRange <= 0D ? 0D : (xValue - lowerPoint.XValue) / xRange;
            var value = lowerPoint.Value + ((upperPoint.Value - lowerPoint.Value) * ratio);
            lines.Add($"{series.Name}: {value:0.#}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildBarTooltip(Point location)
    {
        var xValue = PixelToDomainX(location.X);
        var index = (int)Math.Round(xValue);
        var lines = new List<string>();

        foreach (var series in _series)
        {
            if (index < 0 || index >= series.Points.Count)
            {
                continue;
            }

            var point = series.Points[index];
            if (lines.Count == 0)
            {
                lines.Add(point.Label.Replace('\n', ' '));
            }

            lines.Add($"{series.Name}: {point.Value:0.#}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void PaintCanvas(Graphics graphics, Rectangle bounds)
    {
        graphics.Clear(Color.White);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var axisPen = new Pen(Color.Silver, 1F);
        using var gridPen = new Pen(Color.Gainsboro, 1F);
        using var textBrush = new SolidBrush(Color.FromArgb(48, 56, 66));
        using var font = new Font("Segoe UI", 8.5F);
        using var smallFont = new Font("Segoe UI", 8F);
        using var titleFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        _plotBounds = RectangleF.FromLTRB(bounds.Left + 72, bounds.Top + 24, bounds.Right - 24, bounds.Bottom - 82);
        graphics.DrawRectangle(axisPen, Rectangle.Round(_plotBounds));

        var maxYForRender = GetRenderMaxY();
        for (var i = 0; i <= 4; i++)
        {
            var y = _plotBounds.Bottom - (_plotBounds.Height * i / 4F);
            graphics.DrawLine(gridPen, _plotBounds.Left, y, _plotBounds.Right, y);
            var value = maxYForRender * i / 4D;
            var text = value.ToString("0.#", CultureInfo.CurrentCulture);
            var size = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, textBrush, _plotBounds.Left - size.Width - 8F, y - (size.Height / 2F));
        }

        DrawXAxisLabels(graphics, font, textBrush);

        if (_mode == GraphExplorerMode.Line)
        {
            foreach (var series in _series)
            {
                PaintLineSeries(graphics, series);
            }
        }
        else
        {
            PaintBarSeries(graphics);
        }

        graphics.DrawString(_xAxisTitle, font, textBrush, _plotBounds.Left + (_plotBounds.Width / 2F) - 30F, bounds.Bottom - 22F);
        graphics.TranslateTransform(bounds.Left + 16F, _plotBounds.Top + (_plotBounds.Height / 2F) + 30F);
        graphics.RotateTransform(-90F);
        graphics.DrawString(_yAxisTitle, font, textBrush, 0F, 0F);
        graphics.ResetTransform();

        var legendY = bounds.Top + 8F;
        var legendX = _plotBounds.Right - 220F;
        foreach (var series in _series)
        {
            using var brush = new SolidBrush(series.Color);
            graphics.FillRectangle(brush, legendX, legendY + 4F, 12F, 12F);
            graphics.DrawString(series.Name, smallFont, textBrush, legendX + 18F, legendY);
            legendY += 18F;
        }
    }

    private void PaintLineSeries(Graphics graphics, GraphExplorerSeries series)
    {
        if (series.Points.Count == 0)
        {
            return;
        }

        using var pen = new Pen(series.Color, 2.5F);
        var points = new List<PointF>();
        for (var i = 0; i < series.Points.Count; i++)
        {
            points.Add(new PointF(DomainToPixelX(series.Points[i].XValue), DomainToPixelY(series.Points[i].Value)));
        }

        if (points.Count >= 2)
        {
            graphics.DrawLines(pen, points.ToArray());
        }
        else
        {
            graphics.DrawEllipse(pen, points[0].X - 2F, points[0].Y - 2F, 4F, 4F);
        }
    }

    private void PaintBarSeries(Graphics graphics)
    {
        if (_series.Count == 0)
        {
            return;
        }

        var count = _series.Max(series => series.Points.Count);
        if (count == 0)
        {
            return;
        }

        var barSlotWidth = Math.Max(8F, _plotBounds.Width / Math.Max(1, count));
        var barWidth = Math.Max(4F, (barSlotWidth - 6F) / Math.Max(1, _series.Count));

        for (var index = 0; index < count; index++)
        {
            var centerX = DomainToPixelX(index);
            for (var seriesIndex = 0; seriesIndex < _series.Count; seriesIndex++)
            {
                var series = _series[seriesIndex];
                if (index >= series.Points.Count)
                {
                    continue;
                }

                var point = series.Points[index];
                var height = _plotBounds.Bottom - DomainToPixelY(point.Value);
                var left = centerX - ((barWidth * _series.Count) / 2F) + (seriesIndex * barWidth);
                using var brush = new SolidBrush(series.Color);
                graphics.FillRectangle(brush, left, _plotBounds.Bottom - height, barWidth - 1F, Math.Max(1F, height));
            }
        }
    }

    private void DrawXAxisLabels(Graphics graphics, Font font, Brush textBrush)
    {
        var count = Math.Max(1, _series.Max(series => series.Points.Count));
        if (count == 0)
        {
            return;
        }

        var desiredTicks = Math.Min(6, count);
        for (var i = 0; i < desiredTicks; i++)
        {
            var ratio = desiredTicks == 1 ? 0D : i / (double)(desiredTicks - 1);
            var domainValue = _minX + ((_maxX - _minX) * ratio);
            var x = DomainToPixelX(domainValue);
            using var axisPen = new Pen(Color.Silver, 1F);
            graphics.DrawLine(axisPen, x, _plotBounds.Bottom, x, _plotBounds.Bottom + 4F);

            var label = _mode == GraphExplorerMode.Line
                ? FormatMatchClock(domainValue)
                : GetBarAxisLabel((int)Math.Round(domainValue));

            var labelLines = label.Split('\n');
            var labelY = _plotBounds.Bottom + 6F;
            foreach (var line in labelLines)
            {
                var size = graphics.MeasureString(line, font);
                graphics.DrawString(line, font, textBrush, x - (size.Width / 2F), labelY);
                labelY += size.Height - 2F;
            }
        }
    }

    private string GetBarAxisLabel(int index)
    {
        foreach (var series in _series)
        {
            if (index >= 0 && index < series.Points.Count)
            {
                return series.Points[index].Label;
            }
        }

        return index.ToString(CultureInfo.CurrentCulture);
    }

    private double GetRenderMaxY()
    {
        var values = _series.SelectMany(series => series.Points).Select(point => point.Value).ToList();
        return Math.Max(1D, values.Count == 0 ? 1D : values.Max());
    }

    private float DomainToPixelX(double x)
    {
        var ratio = (_maxX - _minX) <= 0D ? 0D : (x - _minX) / (_maxX - _minX);
        return _plotBounds.Left + (float)(ratio * _plotBounds.Width);
    }

    private float DomainToPixelY(double y)
    {
        var value = _chkLogarithmic.Checked ? Math.Log10(Math.Max(1D, y)) : y;
        var min = _chkLogarithmic.Checked ? Math.Log10(Math.Max(1D, _minY <= 0D ? 1D : _minY)) : _minY;
        var max = _chkLogarithmic.Checked ? Math.Log10(Math.Max(1D, _maxY)) : _maxY;
        var ratio = (max - min) <= 0D ? 0D : (value - min) / (max - min);
        return _plotBounds.Bottom - (float)(ratio * _plotBounds.Height);
    }

    private double PixelToDomainX(float x)
    {
        if (_plotBounds.Width <= 0F)
        {
            return 0D;
        }

        var ratio = (x - _plotBounds.Left) / _plotBounds.Width;
        return _minX + ((_maxX - _minX) * ratio);
    }

    private double PixelToDomainY(float y)
    {
        if (_plotBounds.Height <= 0F)
        {
            return 0D;
        }

        var ratio = (_plotBounds.Bottom - y) / _plotBounds.Height;
        return _minY + ((_maxY - _minY) * ratio);
    }

    private static string FormatMatchClock(double seconds)
    {
        return seconds <= 0D
            ? "0:00"
            : TimeSpan.FromSeconds(seconds).ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
