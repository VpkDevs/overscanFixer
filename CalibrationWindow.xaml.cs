using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OverscanFix;

public partial class CalibrationWindow : Window
{
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private int _leftPx;
    private int _topPx;
    private int _rightPx;
    private int _bottomPx;

    public CalibrationWindow(int screenX, int screenY, int width, int height, int left, int top, int right, int bottom)
    {
        InitializeComponent();

        Left = screenX;
        Top = screenY;
        Width = width;
        Height = height;
        _screenWidth = width;
        _screenHeight = height;
        _leftPx = left;
        _topPx = top;
        _rightPx = right;
        _bottomPx = bottom;

        KeyDown += CalibrationWindow_KeyDown;
        Loaded += (s, e) => DrawCalibrationGrid();
    }

    public void UpdateSafeBounds(int left, int top, int right, int bottom)
    {
        _leftPx = left;
        _topPx = top;
        _rightPx = right;
        _bottomPx = bottom;
        DrawCalibrationGrid();
    }

    private void CalibrationWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Space || e.Key == Key.Enter)
        {
            Close();
        }
    }

    private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void DrawCalibrationGrid()
    {
        OverlayCanvas.Children.Clear();

        // 1. Center Crosshair
        var vCenter = new Line
        {
            X1 = _screenWidth / 2.0, Y1 = 0,
            X2 = _screenWidth / 2.0, Y2 = _screenHeight,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        var hCenter = new Line
        {
            X1 = 0, Y1 = _screenHeight / 2.0,
            X2 = _screenWidth, Y2 = _screenHeight / 2.0,
            Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        OverlayCanvas.Children.Add(vCenter);
        OverlayCanvas.Children.Add(hCenter);

        // 2. Percentage Guides (2.5%, 5.0%, 7.5%)
        double[] percentages = { 2.5, 5.0, 7.5 };
        Color[] pColors = { Color.FromRgb(59, 130, 246), Color.FromRgb(234, 179, 8), Color.FromRgb(239, 68, 68) };

        for (int i = 0; i < percentages.Length; i++)
        {
            double pct = percentages[i];
            double pX = _screenWidth * (pct / 100.0);
            double pY = _screenHeight * (pct / 100.0);

            var pRect = new Rectangle
            {
                Width = _screenWidth - (2 * pX),
                Height = _screenHeight - (2 * pY),
                Stroke = new SolidColorBrush(Color.FromArgb(120, pColors[i].R, pColors[i].G, pColors[i].B)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 6, 4 }
            };
            Canvas.SetLeft(pRect, pX);
            Canvas.SetTop(pRect, pY);
            OverlayCanvas.Children.Add(pRect);

            // Label
            var pLabel = new TextBlock
            {
                Text = $"{pct:F1}% Reference ({Math.Round(pX)}px / {Math.Round(pY)}px)",
                Foreground = new SolidColorBrush(pColors[i]),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Canvas.SetLeft(pLabel, pX + 8);
            Canvas.SetTop(pLabel, pY + 4);
            OverlayCanvas.Children.Add(pLabel);
        }

        // 3. Ruler Ticks Along Each Edge
        int[] ticks = { 10, 20, 30, 40, 50, 60, 75, 100, 125, 150 };
        foreach (int t in ticks)
        {
            // Left edge ticks
            AddRulerTick(0, _screenHeight / 2.0 - t * 3, t, 0, $"{t}px", HorizontalAlignment.Left);
            AddRulerTick(t, 0, 0, 15, $"{t}", HorizontalAlignment.Left, isTop: true);

            // Right edge ticks
            AddRulerTick(_screenWidth - t, 0, 0, 15, $"{t}", HorizontalAlignment.Right, isTop: true);

            // Bottom edge ticks
            AddRulerTick(t, _screenHeight - 15, 0, 15, $"{t}", HorizontalAlignment.Left, isBottom: true);
            AddRulerTick(_screenWidth - t, _screenHeight - 15, 0, 15, $"{t}", HorizontalAlignment.Right, isBottom: true);
        }

        // 4. Current Safe Area Box (Designated Inset)
        double safeX = _leftPx;
        double safeY = _topPx;
        double safeW = Math.Max(100, _screenWidth - _leftPx - _rightPx);
        double safeH = Math.Max(100, _screenHeight - _topPx - _bottomPx);

        var safeRect = new Rectangle
        {
            Width = safeW,
            Height = safeH,
            Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Vibrant Emerald
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(20, 16, 185, 129))
        };
        Canvas.SetLeft(safeRect, safeX);
        Canvas.SetTop(safeRect, safeY);
        OverlayCanvas.Children.Add(safeRect);

        // Safe area dimension badge
        var badgeBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6)
        };
        var badgeText = new TextBlock
        {
            Text = $"SAFE ZONE: {safeW:0} x {safeH:0} px\nLeft: {_leftPx}px ({(_leftPx * 100.0 / _screenWidth):F1}%)  |  Top: {_topPx}px ({(_topPx * 100.0 / _screenHeight):F1}%)\nRight: {_rightPx}px ({(_rightPx * 100.0 / _screenWidth):F1}%)  |  Bottom: {_bottomPx}px ({(_bottomPx * 100.0 / _screenHeight):F1}%)",
            Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };
        badgeBorder.Child = badgeText;
        badgeBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(badgeBorder, safeX + (safeW - badgeBorder.DesiredSize.Width) / 2.0);
        Canvas.SetTop(badgeBorder, safeY + 20);
        OverlayCanvas.Children.Add(badgeBorder);

        // 5. Instruction banner in the center
        var banner = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 24, 24, 27)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 12, 20, 12)
        };
        var bannerText = new TextBlock
        {
            Text = "OVERSCAN CALIBRATION MODE\nAdjust Left/Top/Right/Bottom until the green box meets your screen's visible glass edge.\nClick anywhere or press ESC to close calibration overlay.",
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            TextAlignment = TextAlignment.Center
        };
        banner.Child = bannerText;
        banner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(banner, (_screenWidth - banner.DesiredSize.Width) / 2.0);
        Canvas.SetTop(banner, (_screenHeight - banner.DesiredSize.Height) / 2.0);
        OverlayCanvas.Children.Add(banner);
    }

    private void AddRulerTick(double x, double y, double w, double h, string label, System.Windows.HorizontalAlignment align, bool isTop = false, bool isBottom = false)
    {
        if (isTop || isBottom)
        {
            var line = new Line
            {
                X1 = x, Y1 = isTop ? 0 : _screenHeight - 12,
                X2 = x, Y2 = isTop ? 12 : _screenHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1.5
            };
            OverlayCanvas.Children.Add(line);

            var text = new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 9
            };
            Canvas.SetLeft(text, x - 10);
            Canvas.SetTop(text, isTop ? 14 : _screenHeight - 26);
            OverlayCanvas.Children.Add(text);
        }
        else
        {
            var line = new Line
            {
                X1 = 0, Y1 = y,
                X2 = 12, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1.5
            };
            OverlayCanvas.Children.Add(line);
        }
    }
}
