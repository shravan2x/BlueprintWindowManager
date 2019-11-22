using System;
using System.Collections.Generic;

namespace BlueprintWindowManager
{
    public static class TaskbarInfo
    {
        public static IReadOnlyList<ButtonGroup> GetPrimaryTaskbarInfo()
        {
            int dwError = TTLib.TTLib_Init();
            if (dwError != TTLib.TTLIB_OK)
                throw new Exception($"TTLib_Init() failed with error {dwError}.");

            dwError = TTLib.TTLib_LoadIntoExplorer();
            if (dwError != TTLib.TTLIB_OK)
            {
                TTLib.TTLib_Uninit();
                throw new Exception($"TTLib_LoadIntoExplorer() failed with error {dwError}.");
            }

            if (!TTLib.TTLib_ManipulationStart())
            {
                TTLib.TTLib_UnloadFromExplorer();
                TTLib.TTLib_Uninit();
                throw new Exception("TTLib_ManipulationStart() failed.");
            }

            IntPtr hTaskbar = TTLib.TTLib_GetMainTaskbar();
            if (!TTLib.TTLib_GetButtonGroupCount(hTaskbar, out int buttonGroupCount))
            {
                TTLib.TTLib_ManipulationEnd();
                TTLib.TTLib_UnloadFromExplorer();
                TTLib.TTLib_Uninit();
                throw new Exception("TTLib_GetButtonGroupCount() failed.");
            }

            ButtonGroup[] buttonGroups = new ButtonGroup[buttonGroupCount];
            for (int buttonGroupIndex = 0; buttonGroupIndex < buttonGroupCount; buttonGroupIndex++)
            {
                IntPtr hButtonGroup = TTLib.TTLib_GetButtonGroup(hTaskbar, buttonGroupIndex);

                char[] szAppId = new char[TTLib.MAX_APPID_LENGTH];
                uint appIdLen = TTLib.TTLib_GetButtonGroupAppId(hButtonGroup, szAppId, TTLib.MAX_APPID_LENGTH);
                string appId = new string(szAppId, 0, (int) appIdLen);

                if (!TTLib.TTLib_GetButtonCount(hButtonGroup, out int buttonCount))
                {
                    TTLib.TTLib_ManipulationEnd();
                    TTLib.TTLib_UnloadFromExplorer();
                    TTLib.TTLib_Uninit();
                    throw new Exception("TTLib_GetButtonCount() failed.");
                }

                Button[] buttons = new Button[buttonCount];
                for (int buttonIndex = 0; buttonIndex < buttonCount; buttonIndex++)
                {
                    IntPtr hButton = TTLib.TTLib_GetButton(hButtonGroup, buttonIndex);
                    IntPtr hWnd = TTLib.TTLib_GetButtonWindow(hButton);
                    buttons[buttonIndex] = new Button(hWnd);
                }

                buttonGroups[buttonGroupIndex] = new ButtonGroup(appId, buttons);
            }

            TTLib.TTLib_ManipulationEnd();
            TTLib.TTLib_UnloadFromExplorer();
            TTLib.TTLib_Uninit();

            return buttonGroups;
        }

        public class ButtonGroup
        {
            public string AppId { get; }
            public IReadOnlyList<Button> Buttons { get; }

            public ButtonGroup(string appId, IReadOnlyList<Button> buttons)
            {
                AppId = appId;
                Buttons = buttons;
            }
        }

        public class Button
        {
            public IntPtr WindowHandle { get; }

            public Button(IntPtr windowHandle)
            {
                WindowHandle = windowHandle;
            }
        }
    }
}
