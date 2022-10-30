using Jint;
using Jint.Native.Json;
using Jint.Runtime;
using Newtonsoft.Json;
using Pastel;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jint.Parser;

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
            if (blueprint.EngineInitScripts != null)
            {
                foreach (string engineInitScript in blueprint.EngineInitScripts)
                {
                    try
                    {
                        jsEngine.Execute(engineInitScript);
                    }
                    catch (Exception ex) when (ex is ParserException || ex is JavaScriptException)
                    {
                        Console.WriteLine($"Error executing engine init script ({ex.Message}).".Pastel(Color.Red));
                        return;
                    }
                }
            }

            if (WinApiUtils.IsWindows11OrHigher())
                foreach (LayoutRule rule in blueprint.Rules)
                    if (rule.Filters.TaskbarAppId != null || rule.Filters.TaskbarIndex != null || rule.Filters.TaskbarSubIndex != null)
                        Console.WriteLine($"Ignoring rule '{rule.Name}' as it uses taskbar filters which aren't supported in Windows 11.".Pastel(Color.DarkOrange));

            int programWindowIndex = 0;
            foreach (ProgramWindow programWindow in _programWindowManager.ProgramWindows.OrderBy(x => Path.GetFileName(x.ProgramPath)))
            {
                Console.WriteLine($"[{++programWindowIndex}/{_programWindowManager.ProgramWindows.Count}] Processing window {programWindow.WindowHandle.ToInt32():X16} ({programWindow.WindowTitle.Pastel(Color.CornflowerBlue)}) ...".Pastel(Color.White));
                Console.WriteLine($"\tProgram path: ({programWindow.ProgramPath}).".Pastel(Color.Gray));
                Console.WriteLine($"\tWindow class: {programWindow.WindowClass}.".Pastel(Color.Gray));
                Console.WriteLine($"\tIs tool window: {programWindow.IsToolWindow}.".Pastel(Color.Gray));
                Console.WriteLine($"\tTaskbar position: (index: {programWindow.TaskbarIndex?.ToString() ?? "<none>"}, subindex: {programWindow.TaskbarSubIndex?.ToString() ?? "<none>"}).".Pastel(Color.Gray));
                Console.WriteLine($"\tOriginal rect: (left: {programWindow.WindowRect.left}, top: {programWindow.WindowRect.top}, width: {programWindow.WindowRect.right - programWindow.WindowRect.left}, height: {programWindow.WindowRect.bottom - programWindow.WindowRect.top})".Pastel(Color.Gray));

                LayoutRule? matchedRule = FindMatchingLayoutRule(programWindow, blueprint.Rules);
                if (matchedRule == null)
                    continue;

                WinApiUtils.MonitorInfo? targetMonitorInfo = null;
                if (matchedRule.TargetMonitor != null)
                {
                    if (monitorMapping.ContainsKey(matchedRule.TargetMonitor))
                        targetMonitorInfo = monitorMapping[matchedRule.TargetMonitor];
                    else
                    {
                        Console.WriteLine($"\tNo mapped monitor found by name '{matchedRule.TargetMonitor}'. Skipping window ...".Pastel(Color.Red));
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
                    // Window width and height have to be calculated prior to centering
                    if (matchedRule.TargetRect.Width != null)
                        targetWindowWidth = (int) jsEngine.Execute(matchedRule.TargetRect.Width).GetCompletionValue().AsNumber();
                    if (matchedRule.TargetRect.Height != null)
                        targetWindowHeight = (int) jsEngine.Execute(matchedRule.TargetRect.Height).GetCompletionValue().AsNumber();

                    if (matchedRule.TargetRect.IsCenter)
                    {
                        targetWindowLeft = targetMonitorInfo.MonitorRect.left + ((targetMonitorWidth - targetWindowWidth) / 2);
                        targetWindowTop = targetMonitorInfo.MonitorRect.top + ((targetMonitorHeight - targetWindowHeight) / 2);
                    }

                    // Allow users to override either after centering.
                    if (matchedRule.TargetRect.PosX != null)
                        targetWindowLeft = (int) jsEngine.Execute(matchedRule.TargetRect.PosX).GetCompletionValue().AsNumber();
                    if (matchedRule.TargetRect.PosY != null)
                        targetWindowTop = (int) jsEngine.Execute(matchedRule.TargetRect.PosY).GetCompletionValue().AsNumber();

                    Console.WriteLine($"\tTarget rect: (left: {targetWindowLeft}, top: {targetWindowTop}, width: {targetWindowWidth}, height: {targetWindowHeight})".Pastel(Color.Gray));
                }
                catch (Exception ex) when (ex is ParserException || ex is JavaScriptException)
                {
                    Console.WriteLine($"\tError evaluating rule ({ex.Message}). Skipping window ...".Pastel(Color.Red));
                    continue;
                }

                RECT targetRect = new RECT { left = targetWindowLeft, top = targetWindowTop, right = targetWindowLeft + targetWindowWidth, bottom = targetWindowTop + targetWindowHeight };
                _programWindowManager.MoveWindow(programWindow.WindowHandle, targetRect, matchedRule.TargetState);
            }
        }

        private static LayoutRule? FindMatchingLayoutRule(ProgramWindow programWindow, IReadOnlyList<LayoutRule> layoutRules)
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
                    Console.WriteLine($"\t{ex.Message} Skipping window ...".Pastel(Color.Gray));
                    return null;
                }

                Console.WriteLine($"\tRule '{layoutRule.Name}' matches.".Pastel(Color.ForestGreen));
                return layoutRule;
            }

            return null;
        }
    }
}
