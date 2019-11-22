using System;
using PInvoke;

namespace BlueprintWindowManager
{
    public static class TaskbarContentsPrinter
    {
        public static void PrintButtons(IntPtr hTaskbar, IntPtr hButtonGroup)
        {
            IntPtr hActiveButton = TTLib.TTLib_GetActiveButton(hTaskbar);
            IntPtr hTrackedButton = TTLib.TTLib_GetTrackedButton(hTaskbar);

            int nCount;
            if (TTLib.TTLib_GetButtonCount(hButtonGroup, out nCount))
            {
                Console.WriteLine("\tButton count: {0}", nCount);

                for (int i = 0; i < nCount; i++)
                {
                    IntPtr hButton = TTLib.TTLib_GetButton(hButtonGroup, i);

                    Console.WriteLine("\t{0}: {1:X16}", i, hButton.ToInt32());

                    if (hButton == hActiveButton)
                        Console.WriteLine("\t\t* This is the active button");

                    if (hButton == hTrackedButton)
                        Console.WriteLine("\t\t* This is the mouse-tracked button");

                    IntPtr hWnd = TTLib.TTLib_GetButtonWindow(hButton);

                    Console.WriteLine("\t\tButton window handle: {0:X16}", hWnd.ToInt32());

                    char[] szWindowTitle = new char[256];
                    int windowTitleLen = User32.GetWindowText(hWnd, szWindowTitle, 256);
                    string windowTitle = new string(szWindowTitle, 0, (int) windowTitleLen);

                    Console.WriteLine("\t\tButton window title text: {0}", windowTitle);
                }
            }
        }

        private static void PrintButtonGroups(IntPtr hTaskbar)
        {
            IntPtr hActiveButtonGroup = TTLib.TTLib_GetActiveButtonGroup(hTaskbar);
            IntPtr hTrackedButtonGroup = TTLib.TTLib_GetTrackedButtonGroup(hTaskbar);

            int nCount;
            if (TTLib.TTLib_GetButtonGroupCount(hTaskbar, out nCount))
            {
                Console.WriteLine("Button group count: {0}", nCount);

                for (int i = 0; i < nCount; i++)
                {
                    IntPtr hButtonGroup = TTLib.TTLib_GetButtonGroup(hTaskbar, i);

                    Console.WriteLine("{0}: {1:X16}", i, hButtonGroup.ToInt32());

                    if (hButtonGroup == hActiveButtonGroup)
                        Console.WriteLine("\t* This is the active button group");

                    if (hButtonGroup == hTrackedButtonGroup)
                        Console.WriteLine("\t* This is the mouse-tracked button group");

                    RECT rcButtonGroup;
                    if (!TTLib.TTLib_GetButtonGroupRect(hButtonGroup, out rcButtonGroup))
                        ;//memset(&rcButtonGroup, 0, sizeof(RECT));

                    Console.WriteLine("\tRect: ({0}, {1}) - ({2}, {3})", rcButtonGroup.left, rcButtonGroup.top, rcButtonGroup.right, rcButtonGroup.bottom);

                    TTLib.TTLib_GroupType nButtonGroupType;
                    if (!TTLib.TTLib_GetButtonGroupType(hButtonGroup, out nButtonGroupType))
                        nButtonGroupType = TTLib.TTLib_GroupType.TTLIB_GROUPTYPE_UNKNOWN;

                    Console.WriteLine("\tType: {0}", (int) nButtonGroupType);

                    char[] szAppId = new char[TTLib.MAX_APPID_LENGTH];
                    uint appIdLen = TTLib.TTLib_GetButtonGroupAppId(hButtonGroup, szAppId, TTLib.MAX_APPID_LENGTH);
                    string appId = new string(szAppId, 0, (int) appIdLen);

                    Console.WriteLine("\tAppId: {0}", appId);

                    PrintButtons(hTaskbar, hButtonGroup);
                }
            }
        }

        private static void PrintTaskbarContents(IntPtr hTaskbar)
        {
            Console.WriteLine("Task list window handle: {0:X16}", TTLib.TTLib_GetTaskListWindow(hTaskbar).ToInt32());
            Console.WriteLine("Taskbar window handle: {0:X16}", TTLib.TTLib_GetTaskbarWindow(hTaskbar).ToInt32());
            Console.WriteLine("Taskbar monitor handle: {0:X16}", TTLib.TTLib_GetTaskbarMonitor(hTaskbar).ToInt32());

            PrintButtonGroups(hTaskbar);
        }

        private static bool PrintAllTaskbarsContents()
        {
            Console.WriteLine("Starting taskbar manipulation...");

            if (!TTLib.TTLib_ManipulationStart())
            {
                Console.WriteLine("TTLib_ManipulationStart() failed");
                return false;
            }

            Console.WriteLine();
            Console.WriteLine("Printing main taskbar contents...");

            IntPtr hTaskbar = TTLib.TTLib_GetMainTaskbar();
            PrintTaskbarContents(hTaskbar);

            int nCount;
            if (TTLib.TTLib_GetSecondaryTaskbarCount(out nCount))
            {
                for (int i = 0; i < nCount; i++)
                {
                    Console.WriteLine();
                    Console.WriteLine("Printing secondary taskbar #{0} contents...", i);

                    hTaskbar = TTLib.TTLib_GetSecondaryTaskbar(i);
                    PrintTaskbarContents(hTaskbar);
                }
            }

            TTLib.TTLib_ManipulationEnd();
            return true;
        }

        public static int Run()
        {
            bool bSuccess = false;
            int dwError;

            Console.WriteLine("Initializing 7+ Taskbar Tweaking library...");

            dwError = TTLib.TTLib_Init();
            if (dwError == TTLib.TTLIB_OK)
            {
                Console.WriteLine("Loading 7+ Taskbar Tweaking library into explorer...");

                dwError = TTLib.TTLib_LoadIntoExplorer();
                if (dwError == TTLib.TTLIB_OK)
                {
                    bSuccess = PrintAllTaskbarsContents();
                    TTLib.TTLib_UnloadFromExplorer();
                }
                else
                    Console.WriteLine("TTLib_LoadIntoExplorer() failed with error {0}", dwError);

                TTLib.TTLib_Uninit();
            }
            else
                Console.WriteLine("TTLib_Init() failed with error {0}", dwError);

            return bSuccess ? 0 : 1;
        }
    }
}
