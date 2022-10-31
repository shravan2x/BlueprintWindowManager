using MoreLinq;
using Newtonsoft.Json;
using Pastel;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

namespace BlueprintWindowManager
{
    public class ProgramWindowManager
    {
        private const int BufferSize = 32767; // NTFS max path len. MAX_PATH is deprecated

        private readonly bool _isDryRun;
        private readonly char[] _buffer;
        private readonly IReadOnlyList<WinApiUtils.MonitorInfo> _monitors;

        private IReadOnlyList<TaskbarInfo.ButtonGroup>? _taskbarInfo;
        private IReadOnlyList<IntPtr> _windows;
        private IReadOnlyDictionary<IntPtr, int> _windowProcessIdMap;
        private IReadOnlyDictionary<int, Process> _pidProcessCache;
        private IReadOnlyDictionary<int, string?> _pidPathMap;

        public IReadOnlyList<ProgramWindow> ProgramWindows { get; set; }

        public ProgramWindowManager(IReadOnlyList<WinApiUtils.MonitorInfo> monitors, bool isDryRun)
        {
            _monitors = monitors;
            _isDryRun = isDryRun;

            _buffer = new char[BufferSize];
        }

        public bool IsTaskbarInfoLoaded() => (_taskbarInfo != null);

        public bool LoadProgramWindows()
        {
            if (!WinApiUtils.IsWindows11OrHigher())
            {
                Console.WriteLine("Loading taskbar info ...");
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    LoadTaskbarInfo();
                    Console.WriteLine($"Taskbar info loaded in {stopwatch.ElapsedMilliseconds}ms ({_taskbarInfo.Count} button groups, {_taskbarInfo.Sum(x => x.Buttons.Count)} buttons).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching taskbar info ({ex.Message}).");
                    return false;
                }
            }
            else
                Console.WriteLine("Loading taskbar info is not supported in Windows 11. Skipping ...".Pastel(Color.DarkOrange));

            Console.WriteLine("Fetching program window data ...".Pastel(Color.Gray));
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                Console.WriteLine("Fetching window list ...".Pastel(Color.Gray));
                _windows = WinApiUtils.GetAllWindows().Where(User32.IsWindowVisible).ToList();
                Console.WriteLine("Building window PID map ...".Pastel(Color.Gray));
                BuildWindowProcessIdMap();
                Console.WriteLine("Building PID path map ...".Pastel(Color.Gray));
                BuildPidPathMap();
                Console.WriteLine("Consolidating ProgramWindow instances ...".Pastel(Color.Gray));
                ConsolidateProgramWindowInstances();
                Console.WriteLine($"Fetched program window data in {sw.ElapsedMilliseconds}ms.".Pastel(Color.Gray));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching program window data ({ex.Message}).");
                return false;
            }

            Console.WriteLine($"Identified {ProgramWindows.Count} program windows.".Pastel(Color.Gray));
            return true;
        }

        public void MoveWindow(IntPtr windowHandle, RECT targetRect, WindowState windowState)
        {
            IntPtr srcMonitor = User32.MonitorFromWindow(windowHandle, User32.MonitorOptions.MONITOR_DEFAULTTOPRIMARY);
            IntPtr targetMonitor = User32.MonitorFromRect(ref targetRect, User32.MonitorOptions.MONITOR_DEFAULTTOPRIMARY);

            WinApiUtils.MonitorInfo srcMonitorInfo = _monitors.Single(x => x.MonitorHandle == srcMonitor);
            WinApiUtils.MonitorInfo targetMonitorInfo = _monitors.Single(x => x.MonitorHandle == targetMonitor);

            bool monitorChanged = (srcMonitorInfo.MonitorHandle != targetMonitorInfo.MonitorHandle);
            if (monitorChanged && false)
            {
                Console.WriteLine($"\tAdjusting for monitor DPI change ({srcMonitorInfo.DpiX} -> {targetMonitorInfo.DpiX}, {srcMonitorInfo.DpiY} -> {targetMonitorInfo.DpiY})");
                float effectiveDpiChangeX = ((float)srcMonitorInfo.DpiX / targetMonitorInfo.DpiX);// * ((float) (targetMonitorInfo.Rect.right - targetMonitorInfo.Rect.left) / (srcMonitorInfo.Rect.right - srcMonitorInfo.Rect.left));
                float effectiveDpiChangeY = ((float)srcMonitorInfo.DpiY / targetMonitorInfo.DpiY);// * ((float) (targetMonitorInfo.Rect.bottom - targetMonitorInfo.Rect.top) / (srcMonitorInfo.Rect.bottom - srcMonitorInfo.Rect.top));
                targetRect.right = (int)(targetRect.left + ((targetRect.right - targetRect.left) * effectiveDpiChangeX));
                targetRect.bottom = (int)(targetRect.top + ((targetRect.bottom - targetRect.top) * effectiveDpiChangeY));
                Console.WriteLine($"\tAdjusted rect (right: {targetRect.right}, bottom: {targetRect.bottom})");
            }

            Console.WriteLine($"\tTarget window state is {windowState}.".Pastel(Color.Gray));
            User32.WindowShowStyle targetShowStyle = User32.WindowShowStyle.SW_RESTORE;
            if (windowState == WindowState.Minimized)
                targetShowStyle = User32.WindowShowStyle.SW_MINIMIZE;
            else if (windowState == WindowState.Maximized)
                targetShowStyle = User32.WindowShowStyle.SW_MAXIMIZE;

            User32.WINDOWPLACEMENT targetWindowPlacement = User32.WINDOWPLACEMENT.Create();
            targetWindowPlacement.rcNormalPosition = targetRect;
            targetWindowPlacement.showCmd = targetShowStyle;

            if (_isDryRun)
                return;

            if (!monitorChanged)
            {
                //User32.SetWindowPos(windowHandle, IntPtr.Zero, targetRect.left, targetRect.top, targetRect.right - targetRect.left, targetRect.bottom - targetRect.top, User32.SetWindowPosFlags.SWP_NOZORDER | User32.SetWindowPosFlags.SWP_FRAMECHANGED);
                //User32.ShowWindow(windowHandle, targetShowStyle);

                if (!User32.SetWindowPlacement(windowHandle, targetWindowPlacement))
                    Console.WriteLine("\tError setting window placement.".Pastel(Color.Red));
            }
            else
            {
                targetWindowPlacement.showCmd = User32.WindowShowStyle.SW_RESTORE;
                if (!User32.SetWindowPlacement(windowHandle, targetWindowPlacement))
                    Console.WriteLine("\tError setting window placement 1.".Pastel(Color.Red));

                targetWindowPlacement.showCmd = targetShowStyle;
                if (!User32.SetWindowPlacement(windowHandle, targetWindowPlacement))
                    Console.WriteLine("\tError setting window placement 2.".Pastel(Color.Red));
            }
        }

        private void LoadTaskbarInfo()
        {
            _taskbarInfo = TaskbarInfo.GetPrimaryTaskbarInfo();
        }

        private void BuildWindowProcessIdMap()
        {
            Dictionary<IntPtr, int> windowProcessIdMap = new Dictionary<IntPtr, int>();
            Dictionary<int, Process> pidProcessCache = new Dictionary<int, Process>();

            foreach (IntPtr windowHandle in _windows)
            {
                User32.GetWindowThreadProcessId(windowHandle, out int lpdwProcessId);

                windowProcessIdMap[windowHandle] = lpdwProcessId;
                if (!pidProcessCache.ContainsKey(lpdwProcessId))
                    pidProcessCache[lpdwProcessId] = Process.GetProcessById(lpdwProcessId); // TODO: Does this take time? Can we just merge it with the pid map?
            }

            _windowProcessIdMap = windowProcessIdMap;
            _pidProcessCache = pidProcessCache;
        }

        private void BuildPidPathMap()
        {
            Dictionary<int, string?> pidPathMap = new Dictionary<int, string?>();

            foreach (Process process in _pidProcessCache.Values)
            {
                pidPathMap[process.Id] = null;

                try
                {
                    Kernel32.SafeObjectHandle procHandle = Kernel32.OpenProcess(Kernel32.ProcessAccess.PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
                    if (procHandle == Kernel32.SafeObjectHandle.Null)
                        throw new Exception();
                    if (procHandle.IsInvalid)
                    {
                        Console.WriteLine($"Can't fetch process path for PID {process.Id} as process handle could not be obtained ({process.ProcessName}) (Win32 error {(Win32ErrorCode) Marshal.GetLastWin32Error()}).".Pastel(Color.DarkOrange));
                        continue;
                    }

                    int lpdwSize = _buffer.Length;
                    bool success = Kernel32.QueryFullProcessImageName(procHandle, Kernel32.QueryFullProcessImageNameFlags.None, _buffer, ref lpdwSize);
                    if (!success)
                    {
                        Console.WriteLine($"Can't fetch process path for PID {process.Id} due to error calling QueryFullProcessImageName ({process.ProcessName}) (Win32 error {(Win32ErrorCode) Marshal.GetLastWin32Error()}).".Pastel(Color.DarkOrange));
                        continue;
                    }

                    string processImageName = new string(_buffer, 0, lpdwSize);
                    pidPathMap[process.Id] = processImageName;
                    procHandle.Dispose();
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == Win32ErrorCode.ERROR_ACCESS_DENIED)
                {
                    Console.WriteLine($"Access exception fetching main module of PID {process.Id} ({process.ProcessName}).");
                }
            }

            _pidPathMap = pidPathMap;
        }

        private void ConsolidateProgramWindowInstances()
        {
            List<ProgramWindow> programWindows = new List<ProgramWindow>();
            foreach (IntPtr windowHandle in _windows)
            {
                if (!User32.GetWindowRect(windowHandle, out RECT windowRect))
                {
                    Console.WriteLine($"Error fetching rect of window {windowHandle.ToInt32():X16}");
                    continue;
                }

                int pid = _windowProcessIdMap[windowHandle];
                string? programPath = _pidPathMap[pid];
                string windowTitle = WinApiUtils.GetWindowText(windowHandle);

                uint windowClassLen = User32.RealGetWindowClass(windowHandle, _buffer, (uint) _buffer.Length);
                string windowClass = new string(_buffer, 0, (int) windowClassLen);

                string? taskbarAppId = null;
                uint? taskbarIndex = null, taskbarSubIndex = null;
                if (_taskbarInfo != null)
                {
                    KeyValuePair<int, TaskbarInfo.ButtonGroup> taskbarButtonGroup = _taskbarInfo.Index().FirstOrDefault(x => x.Value.Buttons.Any(y => y.WindowHandle == windowHandle));
                    if (taskbarButtonGroup.Value != null)
                    {
                        taskbarAppId = taskbarButtonGroup.Value.AppId;
                        taskbarIndex = (uint) taskbarButtonGroup.Key;
                        taskbarSubIndex = (uint) taskbarButtonGroup.Value.Buttons.Index().First(x => x.Value.WindowHandle == windowHandle).Key;
                    }
                }

                int exStyle = User32.GetWindowLong(windowHandle, User32.WindowLongIndexFlags.GWL_EXSTYLE);
                bool isToolWindow = ((exStyle & (int) User32.WindowStylesEx.WS_EX_TOOLWINDOW) == (int) User32.WindowStylesEx.WS_EX_TOOLWINDOW);

                programWindows.Add(new ProgramWindow(windowHandle, windowRect, windowTitle, windowClass, programPath, taskbarAppId, taskbarIndex, taskbarSubIndex, isToolWindow));
            }

            ProgramWindows = programWindows;
        }
    }

    public class ProgramWindow
    {
        [JsonIgnore]
        public IntPtr WindowHandle { get; }
        [JsonProperty("windowRect")]
        public RECT WindowRect { get; }
        [JsonProperty("windowTitle")]
        public string WindowTitle { get; }
        [JsonProperty("windowClass", NullValueHandling = NullValueHandling.Ignore)]
        public string WindowClass { get; }
        [JsonProperty("programPath", NullValueHandling = NullValueHandling.Ignore)]
        public string? ProgramPath { get; }
        [JsonProperty("taskbarAppId", NullValueHandling = NullValueHandling.Ignore)]
        public string? TaskbarAppId { get; }
        [JsonProperty("taskbarIndex", NullValueHandling = NullValueHandling.Ignore)]
        public uint? TaskbarIndex { get; }
        [JsonProperty("taskbarSubIndex", NullValueHandling = NullValueHandling.Ignore)]
        public uint? TaskbarSubIndex { get; }
        [JsonProperty("isToolWindow")]
        public bool IsToolWindow { get; }

        public ProgramWindow(
            IntPtr windowHandle,
            RECT windowRect,
            string windowTitle,
            string windowClass,
            string? programPath,
            string? taskbarAppId,
            uint? taskbarIndex,
            uint? taskbarSubIndex,
            bool isToolWindow)
        {
            WindowHandle = windowHandle;
            WindowRect = windowRect;
            WindowTitle = windowTitle;
            WindowClass = windowClass;
            ProgramPath = programPath;
            TaskbarAppId = taskbarAppId;
            TaskbarIndex = taskbarIndex;
            TaskbarSubIndex = taskbarSubIndex;
            IsToolWindow = isToolWindow;
        }
    }
}
