using Jint;
using Jint.Native.Json;
using Jint.Runtime;
using Newtonsoft.Json;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Console = Colorful.Console;

namespace BlueprintWindowManager
{
    public class WindowBlueprintManager
    {
        private readonly IReadOnlyList<WinApiUtils.MonitorInfo> _monitors;
        private readonly ProgramWindowManager _programWindowManager;

        public WindowBlueprintManager(ProgramWindowManager programWindowManager, IReadOnlyList<WinApiUtils.MonitorInfo> monitors)
        {
            _programWindowManager = programWindowManager;
            _monitors = monitors;
        }

        public bool TryValidateMonitorMatch(WindowBlueprint blueprint, out IReadOnlyDictionary<string, WinApiUtils.MonitorInfo> monitorMapping)
        {
            monitorMapping = blueprint.MonitorMatch.ToDictionary(x => x.Key, y => _monitors.SingleOrDefault(z =>
                    z.MonitorRect.left == y.Value.X
                    && z.MonitorRect.top == y.Value.Y
                    && z.MonitorRect.Width() == y.Value.Width
                    && z.MonitorRect.Height() == y.Value.Height
                    && z.DpiX == y.Value.DpiX
                    && z.DpiY == y.Value.DpiY));

            if (monitorMapping.Values.Any(x => x == null))
                return false;
            if (monitorMapping.Values.Count() != monitorMapping.Values.Distinct().Count())
                return false;

            return true;
        }

        public void Apply(WindowBlueprint blueprint)
        {
            if (!TryValidateMonitorMatch(blueprint, out IReadOnlyDictionary<string, WinApiUtils.MonitorInfo> monitorMapping))
                throw new Exception("Monitor match validation failed.");

            Engine jsEngine = new Engine();
            jsEngine.SetValue("console", new { log = new Action<object>(Console.WriteLine) });
            jsEngine.SetValue("programWindows", (new JsonParser(jsEngine)).Parse(JsonConvert.SerializeObject(_programWindowManager.ProgramWindows)));
            foreach (string engineInitScript in blueprint.EngineInitScripts)
                jsEngine.Execute(engineInitScript);

            int programWindowIndex = 0;
            foreach (ProgramWindow programWindow in _programWindowManager.ProgramWindows.OrderBy(x => Path.GetFileName(x.ProgramPath)))
            {
                Console.WriteLine($"[{++programWindowIndex}/{_programWindowManager.ProgramWindows.Count}] Processing window {programWindow.WindowHandle.ToInt32():X16} ({programWindow.WindowTitle}) ...", Color.White);
                Console.WriteLine($"\tProgram path: ({programWindow.ProgramPath}).", Color.Gray);
                Console.WriteLine($"\tWindow class: {programWindow.WindowClass}.", Color.Gray);
                Console.WriteLine($"\tIs tool window: {programWindow.IsToolWindow}.", Color.Gray);
                Console.WriteLine($"\tTaskbar position: (index: {programWindow.TaskbarIndex?.ToString() ?? "<none>"}, subindex: {programWindow.TaskbarSubIndex?.ToString() ?? "<none>"}).", Color.Gray);
                Console.WriteLine($"\tOriginal rect: (left: {programWindow.WindowRect.left}, top: {programWindow.WindowRect.top}, width: {programWindow.WindowRect.right - programWindow.WindowRect.left}, height: {programWindow.WindowRect.bottom - programWindow.WindowRect.top})", Color.Gray);

                LayoutRule matchedRule = FindMatchingLayoutRule(programWindow, blueprint.Rules);
                if (matchedRule == null)
                    continue;

                WinApiUtils.MonitorInfo targetMonitorInfo = null;
                if (matchedRule.TargetMonitor != null)
                {
                    if (monitorMapping.ContainsKey(matchedRule.TargetMonitor))
                        targetMonitorInfo = monitorMapping[matchedRule.TargetMonitor];
                    else
                    {
                        Console.WriteLine($"\tNo mapped monitor found by name '{matchedRule.TargetMonitor}'. Skipping window ...", Color.Red);
                        continue;
                    }
                }
                if (targetMonitorInfo == null)
                {
                    IntPtr monitorHandle = User32.MonitorFromWindow(programWindow.WindowHandle, User32.MonitorOptions.MONITOR_DEFAULTTOPRIMARY);
                    targetMonitorInfo = _monitors.Single(x => x.MonitorHandle == monitorHandle);
                }

                if (!User32.GetWindowRect(programWindow.WindowHandle, out RECT srcWindowRect))
                {
                    Console.WriteLine("\tError fetching current window rect. Skipping window ...");
                    continue;
                }

                int targetMonitorWidth = targetMonitorInfo.MonitorRect.right - targetMonitorInfo.MonitorRect.left;
                int targetMonitorHeight = targetMonitorInfo.MonitorRect.bottom - targetMonitorInfo.MonitorRect.top;
                jsEngine.SetValue("monitorDpiX", targetMonitorInfo.DpiX);
                jsEngine.SetValue("monitorDpiY", targetMonitorInfo.DpiY);
                jsEngine.SetValue("monitorWidth", targetMonitorWidth);
                jsEngine.SetValue("monitorHeight", targetMonitorHeight);
                jsEngine.SetValue("workAreaWidth", targetMonitorInfo.WorkAreaRect.right - targetMonitorInfo.WorkAreaRect.left);
                jsEngine.SetValue("workAreaHeight", targetMonitorInfo.WorkAreaRect.bottom - targetMonitorInfo.WorkAreaRect.top);

                int targetWindowLeft = srcWindowRect.left;
                int targetWindowTop = srcWindowRect.top;
                int targetWindowWidth = srcWindowRect.right - srcWindowRect.left;
                int targetWindowHeight = srcWindowRect.bottom - srcWindowRect.top;
                try
                {
                    if (matchedRule.TargetRect.PosX != null)
                        targetWindowLeft = (int)jsEngine.Execute(matchedRule.TargetRect.PosX).GetCompletionValue().AsNumber();
                    if (matchedRule.TargetRect.PosY != null)
                        targetWindowTop = (int)jsEngine.Execute(matchedRule.TargetRect.PosY).GetCompletionValue().AsNumber();
                    if (matchedRule.TargetRect.Width != null)
                        targetWindowWidth = (int)jsEngine.Execute(matchedRule.TargetRect.Width).GetCompletionValue().AsNumber();
                    if (matchedRule.TargetRect.Height != null)
                        targetWindowHeight = (int)jsEngine.Execute(matchedRule.TargetRect.Height).GetCompletionValue().AsNumber();

                    if (matchedRule.TargetRect.IsCenter)
                    {
                        targetWindowLeft = targetMonitorInfo.MonitorRect.left + ((targetMonitorWidth - targetWindowWidth) / 2);
                        targetWindowTop = targetMonitorInfo.MonitorRect.top + ((targetMonitorHeight - targetWindowHeight) / 2);
                    }
                    Console.WriteLine($"\tTarget rect: (left: {targetWindowLeft}, top: {targetWindowTop}, width: {targetWindowWidth}, height: {targetWindowHeight})", Color.Gray);
                }
                catch (JavaScriptException ex)
                {
                    Console.WriteLine($"\tError calculating targetRect ({ex.Message}). Skipping window ...", Color.Red);
                    continue;
                }

                RECT targetRect = new RECT { left = targetWindowLeft, top = targetWindowTop, right = targetWindowLeft + targetWindowWidth, bottom = targetWindowTop + targetWindowHeight };
                _programWindowManager.MoveWindow(programWindow.WindowHandle, targetRect, matchedRule.TargetState);
            }
        }

        private static LayoutRule FindMatchingLayoutRule(ProgramWindow programWindow, IReadOnlyList<LayoutRule> layoutRules)
        {
            foreach (LayoutRule layoutRule in layoutRules)
            {
                if (!layoutRule.IsEnabled)
                    continue;

                try
                {
                    if (layoutRule.Filters.WindowTitle != null)
                        if (!Regex.IsMatch(programWindow.WindowTitle, layoutRule.Filters.WindowTitle))
                            continue;
                    if (layoutRule.Filters.WindowClass != null)
                        if (!Regex.IsMatch(programWindow.WindowClass, layoutRule.Filters.WindowClass))
                            continue;
                    if (layoutRule.Filters.ProgramPath != null)
                        if (!Regex.IsMatch(programWindow.ProgramPath, layoutRule.Filters.ProgramPath))
                            continue;
                    if (layoutRule.Filters.TaskbarAppId != null)
                        if (!Regex.IsMatch(programWindow.TaskbarAppId, layoutRule.Filters.TaskbarAppId))
                            continue;
                    if (layoutRule.Filters.TaskbarIndex != null)
                        if (!Regex.IsMatch(programWindow.TaskbarIndex?.ToString() ?? String.Empty, layoutRule.Filters.TaskbarIndex))
                            continue;
                    if (layoutRule.Filters.TaskbarSubIndex != null)
                        if (!Regex.IsMatch(programWindow.TaskbarSubIndex?.ToString() ?? String.Empty, layoutRule.Filters.TaskbarSubIndex))
                            continue;
                    if (layoutRule.Filters.IsToolWindow != null)
                        if (layoutRule.Filters.IsToolWindow != programWindow.IsToolWindow)
                            continue;
                }
                catch (Exception ex) when (ex.Message.StartsWith("Invalid pattern")) // It doesn't know RegexParseException for some reason.
                {
                    Console.WriteLine($"\t{ex.Message} Skipping window ...", Color.Gray);
                    return null;
                }

                Console.WriteLine($"\tRule '{layoutRule.Name}' matches.", Color.Gray);
                return layoutRule;
            }

            return null;
        }
    }
}
