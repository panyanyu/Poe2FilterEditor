using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Poe2FilterEditor.Infrastructure
{
    /// <summary>
    /// 把系统标题栏切换为深色，避免亮白标题栏破坏暗色主题（Windows 10 1903+ / Windows 11）。
    /// </summary>
    public static class WindowChromeHelper
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;
        private const int DwmwaBorderColor = 34;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void ApplyDarkTitleBar(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            {
                Apply(window);
                return;
            }

            void OnSourceInitialized(object? sender, EventArgs e)
            {
                window.SourceInitialized -= OnSourceInitialized;
                Apply(window);
            }

            window.SourceInitialized += OnSourceInitialized;
        }

        private static void Apply(Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var dark = 1;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref dark, sizeof(int));
            }

            var caption = ToColorRef(0x17, 0x13, 0x10);
            DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, sizeof(int));

            var border = ToColorRef(0x33, 0x2C, 0x24);
            DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(int));

            var text = ToColorRef(0xE8, 0xE2, 0xD6);
            DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, sizeof(int));
        }

        private static int ToColorRef(byte r, byte g, byte b) => r | (g << 8) | (b << 16);
    }
}
