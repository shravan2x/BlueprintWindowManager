using CommandLine;
using Newtonsoft.Json;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Console = Colorful.Console;

namespace BlueprintWindowManager
{
    internal static class Program
    {
        public const string WindowBlueprintFileExtension = ".windowblueprint.json";

        private static void Main(string[] args)
        {
            ParserResult<Options> optionsResult = Parser.Default.ParseArguments<Options>(args);
            if (optionsResult.Tag != ParserResultType.Parsed)
                return;

            Options options = (optionsResult as Parsed<Options>)?.Value;

            User32.SetProcessDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
            WinApiUtils.SetDllDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, (Environment.Is64BitProcess ? "x64" : "x86")));

            Console.WriteLine("Fetching monitor list ...");
            IReadOnlyList<WinApiUtils.MonitorInfo> monitors = WinApiUtils.GetAllMonitorInfos();
            foreach (WinApiUtils.MonitorInfo monitor in monitors)
                Console.WriteLine($"Found '{monitor.Name}' (x: {monitor.MonitorRect.left}, y: {monitor.MonitorRect.top}, width: {monitor.MonitorRect.right - monitor.MonitorRect.left}, height: {monitor.MonitorRect.bottom - monitor.MonitorRect.top}, dpiX: {monitor.DpiX}, dpiY: {monitor.DpiY}).");

            ProgramWindowManager programWindowManager = new ProgramWindowManager(monitors, options.IsDryRun);
            WindowBlueprintManager windowBlueprintManager = new WindowBlueprintManager(programWindowManager, monitors);

            WindowBlueprint blueprint = GetPreferredBlueprint(options, windowBlueprintManager);
            if (blueprint == null)
                return; // We've already displayed an error message in the called function.

            if (!programWindowManager.LoadProgramWindows())
                return; // We've already displayed an error message in the called function.

            windowBlueprintManager.Apply(blueprint);
        }

        private static WindowBlueprint GetPreferredBlueprint(Options options, WindowBlueprintManager windowBlueprintManager)
        {
            if (options.BlueprintFile != null)
            {
                WindowBlueprint blueprint;
                try
                {
                    blueprint = JsonConvert.DeserializeObject<WindowBlueprint>(File.ReadAllText(options.BlueprintFile));
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error reading layout definition {Path.GetFileName(options.BlueprintFile)} ({ex.Message}).", Color.Red);
                    return null;
                }

                if (!windowBlueprintManager.TryValidateMonitorMatch(blueprint, out IReadOnlyDictionary<string, WinApiUtils.MonitorInfo> monitorMapping))
                {
                    if (monitorMapping.Values.Any(x => x == null))
                        Console.WriteLine("Monitor contained invalid mapping. Cannot proceed.", Color.Red);
                    if (monitorMapping.Values.Count() != monitorMapping.Values.Distinct().Count())
                        Console.WriteLine("Monitor contained duplicate mappings. Cannot proceed.", Color.Red);
                    return null;
                }

                return blueprint;
            }

            Console.WriteLine("Reading layout definitions ...");
            IEnumerable<string> windowLayouts = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), $"*{WindowBlueprintFileExtension}");
            List<WindowBlueprint> validBlueprints = new List<WindowBlueprint>();
            foreach (string windowLayout in windowLayouts)
            {
                try
                {
                    WindowBlueprint blueprint = JsonConvert.DeserializeObject<WindowBlueprint>(File.ReadAllText(windowLayout));
                    Console.WriteLine($"Loaded layout definition '{blueprint.Name}' ({blueprint.Rules.Count} rules, {Path.GetFileName(windowLayout)}).");

                    if (windowBlueprintManager.TryValidateMonitorMatch(blueprint, out _))
                        validBlueprints.Add(blueprint);
                    else
                        Console.WriteLine("\tSkipping as monitor match failed.");
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error reading layout definition {Path.GetFileName(windowLayout)} ({ex.Message}).", Color.Red);
                }
            }

            if (validBlueprints.Count == 0)
            {
                Console.WriteLine("No valid layouts available.");
                return null;
            }
            else if (validBlueprints.Count > 1)
            {
                Console.WriteLine("Layouts available:");
                for (int index = 0; index < validBlueprints.Count; index++)
                    Console.WriteLine($"\t{index}: {validBlueprints[index].Name}");
                Console.Write("Which would you like to apply? ");

                try
                {
                    int chosenLayoutIndex = Int32.Parse(Console.ReadLine() ?? throw new InvalidOperationException());
                    return validBlueprints[chosenLayoutIndex];
                }
                catch
                {
                    Console.WriteLine("Invalid selection. Terminating.", Color.Red);
                    return null;
                }
            }

            return validBlueprints[0];
        }

        private class Options
        {
            [Value(0, Required = false, MetaName = "blueprint file", HelpText = "Set bwindow blueprint file to use.")]
            public string BlueprintFile { get; private set; }

            [Option("dryrun", Required = false, HelpText = "Set run mode to dryrun.")]
            public bool IsDryRun { get; private set; }
        }
    }
}
