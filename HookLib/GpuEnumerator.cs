using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace InjectorFx
{
    // 公共可见，供 Program 调用
    public static class GpuEnumerator
    {
        private static readonly Guid IID_IDXGIFactory = new Guid("7B7166EC-21C7-44AE-B21A-C9AE321AE369");
        private static readonly Guid IID_IDXGIAdapter = new Guid("2411E7E1-12AC-4CCF-BD14-9798E8534DC0");

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory(ref Guid riid, out IntPtr ppFactory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DXGI_ADAPTER_DESC
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public IntPtr AdapterLuid;
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("7B7166EC-21C7-44AE-B21A-C9AE321AE369")]
        private interface IDXGIFactory
        {
            int SetPrivateData();
            int SetPrivateDataInterface();
            int GetPrivateData();
            int GetParent(ref Guid riid, out IntPtr parent);
            int EnumAdapters(uint adapter, out IntPtr ppAdapter);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("2411E7E1-12AC-4CCF-BD14-9798E8534DC0")]
        private interface IDXGIAdapter
        {
            int SetPrivateData();
            int SetPrivateDataInterface();
            int GetPrivateData();
            int GetParent(ref Guid riid, out IntPtr parent);
            int EnumOutputs(); // skip
            int GetDesc(out DXGI_ADAPTER_DESC desc);
        }

        public class GpuInfo
        {
            public int Index;
            public string Description;
            public double DedicatedMB;
            public uint VendorId;
            public uint DeviceId;
            public override string ToString()
                => $"[{Index}] {Description} (VRAM {DedicatedMB:0}MB Vid=0x{VendorId:X4} Dev=0x{DeviceId:X4})";
        }

        public static List<GpuInfo> Enumerate()
        {
            var list = new List<GpuInfo>();
            try
            {
                Guid g = IID_IDXGIFactory; // 复制到局部，避免 ref readonly 报错
                if (CreateDXGIFactory(ref g, out var pFactory) != 0 || pFactory == IntPtr.Zero)
                    return list;

                var factory = (IDXGIFactory)Marshal.GetObjectForIUnknown(pFactory);
                uint i = 0;
                while (true)
                {
                    IntPtr pAdapter;
                    int hr = factory.EnumAdapters(i, out pAdapter);
                    if (hr != 0 || pAdapter == IntPtr.Zero) break;
                    try
                    {
                        var adapter = (IDXGIAdapter)Marshal.GetObjectForIUnknown(pAdapter);
                        if (adapter.GetDesc(out DXGI_ADAPTER_DESC desc) == 0)
                        {
                            list.Add(new GpuInfo
                            {
                                Index = (int)i,
                                Description = desc.Description?.Trim(),
                                DedicatedMB = desc.DedicatedVideoMemory.ToUInt64() / 1024.0 / 1024.0,
                                VendorId = desc.VendorId,
                                DeviceId = desc.DeviceId
                            });
                        }
                    }
                    finally { Marshal.Release(pAdapter); }
                    i++;
                }
                Marshal.Release(pFactory);
            }
            catch
            {
                // 忽略错误
            }
            return list;
        }
    }
}