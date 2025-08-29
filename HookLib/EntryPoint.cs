using System;
using EasyHook;

namespace HookLib
{
    public class EntryPoint : IEntryPoint
    {
        private readonly HookConfig _config;

        public EntryPoint(RemoteHooking.IContext ctx, HookConfig config)
        {
            _config = config;
            DependencyResolver.Install();
            Logging.Info("[EntryPoint] Constructed.");
        }

        public void Run(RemoteHooking.IContext ctx, HookConfig config)
        {
            try
            {
                Logging.Info("[EntryPoint] Run() starting bootstrap...");
                if (config != null)
                    HookBootstrap.ApplyExternalConfig(config);
                else
                    Logging.Warn("[EntryPoint] Config is null, using defaults/env.");

                // 提前尝试加载 YOLO（会打印依赖问题）
                try { YoloProcessor.EnsureInit(); }
                catch (Exception ex) { Logging.Exception("[EntryPoint] Pre-init YOLO failed", ex); }

                HookBootstrap.Start();
            }
            catch (Exception ex)
            {
                Logging.Exception("[EntryPoint] Run() exception", ex);
            }

            // 保持驻留
            try
            {
                while (true) System.Threading.Thread.Sleep(500);
            }
            catch { }
        }
    }
}