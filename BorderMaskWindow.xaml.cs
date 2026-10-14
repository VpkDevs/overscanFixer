using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace OverscanFix;

public partial class BorderMaskWindow : Window
{
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    public BorderMaskWindow(int screenX, int screenY, int width, int height)
    {
        InitializeComponent();

        Left = screenX;
        Top = screenY;
        Width = width;
        Height = height;
        _screenWidth = width;
        _screenHeight = height;

        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeClickThrough(hwnd);
        };
    }

    public void UpdateBorders(int left, int top, int right, int bottom)
    {
        // Left bar
        LeftBar.Width = Math.Max(0, left);
        LeftBar.Height = _screenHeight;

        // Top bar
        TopBar.Width = _screenWidth;
        TopBar.Height = Math.Max(0, top);

        // Right bar
        RightBar.Width = Math.Max(0, right);
        RightBar.Height = _screenHeight;
        Canvas.SetLeft(RightBar, Math.Max(0, _screenWidth - right));

        // Bottom bar
        BottomBar.Width = _screenWidth;
        BottomBar.Height = Math.Max(0, bottom);
        Canvas.SetTop(BottomBar, Math.Max(0, _screenHeight - bottom));
    }
}
