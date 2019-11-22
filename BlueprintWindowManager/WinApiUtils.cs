using PInvoke;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BlueprintWindowManager
{
#pragma warning disable CA1401 // P/Invokes should not be visible
    public static class WinApiUtils
    {
        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref User32.MONITORINFOEX lpmi);

        [DllImport("shcore.dll")]
        internal static extern uint GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        /// <summary> Get the text for the window pointed to by hWnd </summary>
        public static string GetWindowText(IntPtr hWnd)
        {
            int size = User32.GetWindowTextLength(hWnd);
            if (size > 0)
            {
                char[] builder = new char[size + 1];
                int windowTextLen = User32.GetWindowText(hWnd, builder, size + 1);
                return new string(builder, 0, windowTextLen);
            }

            return String.Empty;
        }

        public static IReadOnlyList<IntPtr> GetAllWindows()
        {
            List<IntPtr> windows = new List<IntPtr>();
            User32.EnumWindows(delegate (IntPtr wnd, IntPtr param)
            {
                windows.Add(wnd);
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public static IReadOnlyList<MonitorInfo> GetAllMonitorInfos()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                string deviceName = null;
                RECT workAreaRect = default;
                unsafe
                {
                    User32.MONITORINFOEX monitorInfoEx = User32.MONITORINFOEX.Create();

                    bool success = GetMonitorInfo(hMonitor, ref monitorInfoEx);
                    if (success)
                    {
                        deviceName = new string(monitorInfoEx.DeviceName);
                        workAreaRect = monitorInfoEx.WorkArea;
                    }
                    else
                        Console.WriteLine($"Error in GetMonitorInfo ({Marshal.GetLastWin32Error()}).");
                }

                HResult.Code getDpiResult = (HResult.Code) GetDpiForMonitor(hMonitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
                if (getDpiResult != HResult.Code.S_OK)
                    Console.WriteLine($"Error in GetDpiForMonitor ({getDpiResult}).");

                monitors.Add(new MonitorInfo(hMonitor, deviceName, lprcMonitor, workAreaRect, dpiX, dpiY));
                return true;
            }, IntPtr.Zero);

            return monitors;
        }

        public static bool Contains(this RECT rect, float x, float y)
        {
            return (x >= rect.left && x < rect.right && y >= rect.top && y < rect.bottom);
        }

        public static int Width(this RECT rect)
        {
            return (rect.right - rect.left);
        }

        public static int Height(this RECT rect)
        {
            return (rect.bottom - rect.top);
        }

        public enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2,
        }

        public class MonitorInfo
        {
            public IntPtr MonitorHandle { get; }
            public string Name { get; }
            public RECT MonitorRect { get; }
            public RECT WorkAreaRect { get; }
            public uint DpiX { get; }
            public uint DpiY { get; }

            public MonitorInfo(IntPtr monitorHandle, string name, RECT monitorRect, RECT workAreaRect, uint dpiX, uint dpiY)
            {
                MonitorHandle = monitorHandle;
                Name = name;
                MonitorRect = monitorRect;
                WorkAreaRect = workAreaRect;
                DpiX = dpiX;
                DpiY = dpiY;
            }
        }
    }
#pragma warning restore CA1401 // P/Invokes should not be visible
}
