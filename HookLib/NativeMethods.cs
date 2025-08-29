using System;
using System.Runtime.InteropServices;

namespace HookLib
{
    internal static class NativeMethods
    {

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);


        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        public const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        public const int SW_HIDE = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            int exStyle, string lpClassName, string lpWindowName,
            int dwStyle, int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr CreateHiddenWindow(string cls)
        {
            IntPtr WndProc(IntPtr h, uint m, IntPtr w, IntPtr l) => DefWindowProc(h, m, w, l);
            var del = new WndProcDelegate(WndProc);
            var ptr = Marshal.GetFunctionPointerForDelegate(del);

            var wc = new WNDCLASS
            {
                lpfnWndProc = ptr,
                lpszClassName = cls,
                hInstance = Marshal.GetHINSTANCE(typeof(NativeMethods).Module)
            };
            RegisterClassW(ref wc);
            var hwnd = CreateWindowExW(0, cls, cls, WS_OVERLAPPEDWINDOW,
                0, 0, 160, 120, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            ShowWindow(hwnd, SW_HIDE);
            return hwnd;
        }
    }
}