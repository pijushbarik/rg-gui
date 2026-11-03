using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace rg_gui
{
    // Scrolls the ScrollViewer under the mouse when the mouse wheel is tilted horizontally.
    internal static class HorizontalWheelScroll
    {
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const double SCROLL_STEP = 40;

        public static void Attach(Window window)
        {
            window.SourceInitialized += (_, _) =>
            {
                if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
                {
                    hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                        WndProc(window, msg, wParam, lParam, ref handled));
                }
            };
        }

        private static IntPtr WndProc(Window window, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_MOUSEHWHEEL)
            {
                return IntPtr.Zero;
            }

            // The high-order word of wParam holds the signed horizontal wheel delta.
            var delta = (short)((wParam.ToInt64() >> 16) & 0xffff);

            var mousePosition = window.PointFromScreen(new Point(
                (short)(lParam.ToInt64() & 0xffff),
                (short)((lParam.ToInt64() >> 16) & 0xffff)
            ));

            var hit = window.InputHitTest(mousePosition) as DependencyObject;
            var scrollViewer = FindAncestorScrollViewer(hit);

            if (scrollViewer != null)
            {
                // Positive delta = tilt right, negative = tilt left.
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + (delta > 0 ? SCROLL_STEP : -SCROLL_STEP));
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static ScrollViewer? FindAncestorScrollViewer(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                element = (element is Visual || element is Visual3D)
                    ? VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            }
            return null;
        }
    }
}
