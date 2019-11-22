using System;
using System.Runtime.InteropServices;
using PInvoke;

namespace BlueprintWindowManager
{
#pragma warning disable CA1401 // P/Invokes should not be visible
    public static class TTLib
    {
        //////////////////////////////////////////////////////////////////////////
        // Initialization

        public const int TTLIB_OK = 0;

        public enum TTLib_Init_Errors
        {
            TTLIB_ERR_INIT_ALREADY_INITIALIZED = 1,
            TTLIB_ERR_INIT_REGISTER_MESSAGE,
            TTLIB_ERR_INIT_FILE_MAPPING,
            TTLIB_ERR_INIT_VIEW_MAPPING,
        }

        public enum TTLib_LoadIntoExplorer_Errors
        {
            TTLIB_ERR_EXE_NOT_INITIALIZED = 1,
            TTLIB_ERR_EXE_ALREADY_LOADED,
            TTLIB_ERR_EXE_FIND_TASKBAR,
            TTLIB_ERR_EXE_OPEN_PROCESS,
            TTLIB_ERR_EXE_VIRTUAL_ALLOC,
            TTLIB_ERR_EXE_WRITE_PROC_MEM,
            TTLIB_ERR_EXE_CREATE_REMOTE_THREAD,
            TTLIB_ERR_EXE_READ_PROC_MEM,

            TTLIB_ERR_INJ_BEFORE_RUN = 101,
            TTLIB_ERR_INJ_BEFORE_GETMODULEHANDLE,
            TTLIB_ERR_INJ_BEFORE_LOADLIBRARY,
            TTLIB_ERR_INJ_BEFORE_GETPROCADDR,
            TTLIB_ERR_INJ_BEFORE_LIBINIT,
            TTLIB_ERR_INJ_GETMODULEHANDLE,
            TTLIB_ERR_INJ_LOADLIBRARY,
            TTLIB_ERR_INJ_GETPROCADDR,

            TTLIB_ERR_LIB_INIT_ALREADY_CALLED = 201,
            TTLIB_ERR_LIB_LIB_VER_MISMATCH,
            TTLIB_ERR_LIB_WIN_VER_MISMATCH,
            TTLIB_ERR_LIB_VIEW_MAPPING,
            TTLIB_ERR_LIB_FIND_IMPORT,
            TTLIB_ERR_LIB_WND_TASKBAR,
            TTLIB_ERR_LIB_WND_TASKSW,
            TTLIB_ERR_LIB_WND_TASKLIST,
            TTLIB_ERR_LIB_WND_THUMB,
            TTLIB_ERR_LIB_MSG_DLL_INIT,
            TTLIB_ERR_LIB_WAITTHREAD,

            TTLIB_ERR_LIB_EXTHREAD_MINHOOK = 301,
            TTLIB_ERR_LIB_EXTHREAD_COMFUNCHOOK,
            TTLIB_ERR_LIB_EXTHREAD_REFRESHTASKBAR,
            TTLIB_ERR_LIB_EXTHREAD_MINHOOK_APPLY,
        }

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int TTLib_Init();
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_Uninit();

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int TTLib_LoadIntoExplorer();
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_IsLoadedIntoExplorer();
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_UnloadFromExplorer();

        //////////////////////////////////////////////////////////////////////////
        // Manipulation

        public enum TTLib_GroupType
        {
#pragma warning disable CA1712 // Do not prefix enum values with type name
            TTLIB_GROUPTYPE_UNKNOWN = 0,
            TTLIB_GROUPTYPE_NORMAL,
            TTLIB_GROUPTYPE_PINNED,
            TTLIB_GROUPTYPE_COMBINED,
            TTLIB_GROUPTYPE_TEMPORARY,
#pragma warning restore CA1712 // Do not prefix enum values with type name
        }

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_ManipulationStart();
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_ManipulationEnd();

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetMainTaskbar();
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetSecondaryTaskbarCount(out int pnCount);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetSecondaryTaskbar(int nIndex);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetTaskListWindow(IntPtr hTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetTaskbarWindow(IntPtr hTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetTaskbarMonitor(IntPtr hTaskbar);

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetButtonGroupCount(IntPtr hTaskbar, out int pnCount);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetButtonGroup(IntPtr hTaskbar, int nIndex);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetActiveButtonGroup(IntPtr hTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetTrackedButtonGroup(IntPtr hTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_ButtonGroupMove(IntPtr hTaskbar, int nIndexFrom, int nIndexTo);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetButtonGroupTaskbar(IntPtr hButtonGroup, out IntPtr phTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetButtonGroupRect(IntPtr hButtonGroup, out RECT pRect);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetButtonGroupType(IntPtr hButtonGroup, out TTLib_GroupType pnType);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern uint TTLib_GetButtonGroupAppId(IntPtr hButtonGroup, char[] pszAppId, uint nMaxSize);

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetButtonCount(IntPtr hButtonGroup, out int pnCount);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetButton(IntPtr hButtonGroup, int nIndex);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetActiveButton(IntPtr hTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetTrackedButton(IntPtr hTaskbar);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_ButtonMoveInButtonGroup(IntPtr hButtonGroup, int nIndexFrom, int nIndexTo);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TTLib_GetButtonWindow(IntPtr hButton);

        //////////////////////////////////////////////////////////////////////////
        // Lists

        public enum TTLib_List
        {
#pragma warning disable CA1712 // Do not prefix enum values with type name
            TTLIB_LIST_LABEL = 0,
            TTLIB_LIST_GROUP,
            TTLIB_LIST_GROUPPINNED,
            TTLIB_LIST_COMBINE,
#pragma warning restore CA1712 // Do not prefix enum values with type name
        }

        public enum TTLib_List_Value
        {
            TTLIB_LIST_LABEL_NEVER = 0,
            TTLIB_LIST_LABEL_ALWAYS,

            TTLIB_LIST_GROUP_NEVER = 0,
            TTLIB_LIST_GROUP_ALWAYS,

            TTLIB_LIST_GROUPPINNED_NEVER = 0,
            TTLIB_LIST_GROUPPINNED_ALWAYS,

            TTLIB_LIST_COMBINE_NEVER = 0,
            TTLIB_LIST_COMBINE_ALWAYS,
        }

        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_AddAppIdToList(TTLib_List nList, [MarshalAs(UnmanagedType.LPWStr)] string pszAppId, TTLib_List_Value nListValue);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_RemoveAppIdFromList(TTLib_List nList, [MarshalAs(UnmanagedType.LPWStr)] string pszAppId);
        [DllImport(@"TTLib.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool TTLib_GetAppIdListValue(TTLib_List nList, [MarshalAs(UnmanagedType.LPWStr)] string pszAppId, out TTLib_List_Value pnListValue);

        //////////////////////////////////////////////////////////////////////////
        // Other

        public const int MAX_APPID_LENGTH = 260;
    }
#pragma warning restore CA1401 // P/Invokes should not be visible
}
