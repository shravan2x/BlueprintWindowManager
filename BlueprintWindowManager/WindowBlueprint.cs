using Newtonsoft.Json;
using System.Collections.Generic;

namespace BlueprintWindowManager
{
    public class WindowBlueprint
    {
        [JsonProperty("name")]
        public string Name { get; private set; }
        [JsonProperty("comment")]
        public string Comment { get; private set; }
        [JsonProperty("monitorMatch", Required = Required.Always)]
        public IReadOnlyDictionary<string, MonitorMatcher> MonitorMatch { get; private set; }
        [JsonProperty("engineInitScripts")]
        public IReadOnlyList<string>? EngineInitScripts { get; private set; }
        [JsonProperty("rules")]
        public IReadOnlyList<LayoutRule> Rules { get; private set; }
    }

    public class MonitorMatcher
    {
        [JsonProperty("x")]
        public int X { get; private set; }
        [JsonProperty("y")]
        public int Y { get; private set; }
        [JsonProperty("width")]
        public int Width { get; private set; }
        [JsonProperty("height")]
        public int Height { get; private set; }
        [JsonProperty("dpiX")]
        public int DpiX { get; private set; }
        [JsonProperty("dpiY")]
        public int DpiY { get; private set; }
    }

    public class LayoutRule
    {
        [JsonProperty("enabled")]
        public bool IsEnabled { get; private set; }
        [JsonProperty("name")]
        public string Name { get; private set; }
        [JsonProperty("comment")]
        public string Comment { get; private set; }
        [JsonProperty("filters")]
        public WindowFilters Filters { get; private set; }
        [JsonProperty("targetMonitor")]
        public string? TargetMonitor { get; private set; }
        [JsonProperty("targetRect")]
        public WindowRect TargetRect { get; private set; }
        [JsonProperty("targetState")]
        public WindowState TargetState { get; private set; }
    }

    public class WindowFilters
    {
        [JsonProperty("windowTitle")]
        public string? WindowTitle { get; private set; }
        [JsonProperty("windowClass")]
        public string? WindowClass { get; private set; }
        [JsonProperty("programPath")]
        public string? ProgramPath { get; private set; }
        [JsonProperty("taskbarAppId")]
        public string? TaskbarAppId { get; private set; }
        [JsonProperty("taskbarIndex")]
        public string? TaskbarIndex { get; private set; }
        [JsonProperty("taskbarSubIndex")]
        public string? TaskbarSubIndex { get; private set; }
        [JsonProperty("isToolWindow")]
        public bool? IsToolWindow { get; private set; }
    }

    public class WindowRect
    {
        [JsonProperty("posX")]
        public string? PosX { get; private set; }
        [JsonProperty("posY")]
        public string? PosY { get; private set; }
        [JsonProperty("width")]
        public string? Width { get; private set; }
        [JsonProperty("height")]
        public string? Height { get; private set; }
        [JsonProperty("center")]
        public bool IsCenter { get; private set; }
    }

    public enum WindowState
    {
        Restored,
        Minimized,
        Maximized
    }
}
