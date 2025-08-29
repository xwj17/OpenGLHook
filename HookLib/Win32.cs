using System;
using System.Runtime.InteropServices;

namespace HookLib
{
    internal static class Win32
    {
        [DllImport("kernel32.dll")] internal static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    }
}