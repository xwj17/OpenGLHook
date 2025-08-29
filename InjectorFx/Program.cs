using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using EasyHook;
using HookLib;

namespace InjectorFx
{
    internal class Program
    {
        private const string CONFIG_FILE = "injector_config.txt";

        static void Main(string[] args)
        {
            Console.Title = "Hook Injector (GPU/CPU + Dynamic Interval)";
            Console.WriteLine("=== Hook Injector (OpenGL + YOLO) - Attach Mode ===\n");

            var cfg = LoadConfig() ?? new HookConfig();

            string injectorDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            cfg.ModelPath = Path.Combine(injectorDir, "yolo.onnx");

            // 枚举设备
            string cpuName = GetCpuName();
            var gpus = GpuEnumerator.Enumerate();
            PrintDeviceList(cpuName, gpus);

            Console.Write("使用当前/上次配置直接注入? (Y/n): ");
            var ans = Console.ReadLine();
            bool useDefault = string.IsNullOrWhiteSpace(ans) || ans.StartsWith("y", StringComparison.OrdinalIgnoreCase);

            if (!useDefault)
            {
                SelectDevice(cpuName, gpus, cfg);
                InteractiveEditOther(cfg);
            }

            if (!File.Exists(cfg.ModelPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[警告] 模型文件缺失: " + cfg.ModelPath);
                Console.ResetColor();
            }

            DumpConfig(cfg);

            Console.Write("确认注入? (Y/n): ");
            var confirm = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(confirm) && confirm.StartsWith("n", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("取消。");
                return;
            }

            SaveConfig(cfg);

            int pid = SelectPccProcess();
            if (pid <= 0)
            {
                Console.WriteLine("未选择有效进程。");
                return;
            }

            string hookLibPath = ResolveHookLibPath();
            if (hookLibPath == null)
            {
                Console.WriteLine("未找到 HookLib.dll。");
                return;
            }

            Console.WriteLine($"准备注入 PID={pid}  HookLib={hookLibPath}");
            try
            {
                RemoteHooking.Inject(pid, null, hookLibPath, cfg);
                Console.WriteLine("已请求注入。");
            }
            catch (Exception ex)
            {
                Console.WriteLine("注入失败: " + ex);
            }

            Console.WriteLine("按回车退出...");
            Console.ReadLine();
        }

        private static void PrintDeviceList(string cpuName, List<GpuEnumerator.GpuInfo> gpus)
        {
            Console.WriteLine("可用计算设备:");
            Console.WriteLine("  [C] " + cpuName);
            foreach (var g in gpus)
                Console.WriteLine($"  [{g.Index}] {g.Description} (VRAM {g.DedicatedMB:0}MB)");
            Console.WriteLine();
        }

        private static void SelectDevice(string cpuName, List<GpuEnumerator.GpuInfo> gpus, HookConfig cfg)
        {
            Console.Write($"选择计算设备 (C=CPU, 数字=GPU, 回车保持[{(cfg.UseCPU ? "C" : (cfg.GpuDeviceId < 0 ? "autoGPU" : cfg.GpuDeviceId.ToString()))}]): ");
            var s = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(s)) return;
            if (s.Equals("c", StringComparison.OrdinalIgnoreCase))
            {
                cfg.UseCPU = true;
                cfg.ForceDirectML = false;
                cfg.GpuDeviceId = -1;
                Console.WriteLine("选择 CPU");
            }
            else if (int.TryParse(s, out int id) && id >= 0 && (gpus.Count == 0 || id < gpus.Count))
            {
                cfg.UseCPU = false;
                cfg.ForceDirectML = true;
                cfg.GpuDeviceId = id;
                Console.WriteLine("选择 GPU#" + id);
            }
            else
            {
                Console.WriteLine("输入无效，保持原值。");
            }
        }

        private static void InteractiveEditOther(HookConfig cfg)
        {
            Console.WriteLine("参数配置（回车保留原值）:");
            cfg.DetectionInterval = AskInt("基础检测间隔(0=每帧)", cfg.DetectionInterval, 0, 100);
            cfg.ConfThreshold = AskFloat("置信度阈值", cfg.ConfThreshold, 0f, 1f);
            cfg.NmsThreshold = AskFloat("NMS IoU 阈值", cfg.NmsThreshold, 0f, 1f);
            cfg.DisableOverlay = AskBool("禁用Overlay", cfg.DisableOverlay);
            cfg.LogDetailFrames = AskInt("详细日志前N帧", cfg.LogDetailFrames, 0, 10000);
            cfg.Verbose = AskBool("Verbose调试日志", cfg.Verbose);          // 重新加入是否输出调试日志选项
            if (!cfg.UseCPU)
                cfg.ForceDirectML = AskBool("DirectML(GPU) 加速", cfg.ForceDirectML);
            cfg.ShowPerfMetrics = AskBool("显示性能指标文字", cfg.ShowPerfMetrics);
            cfg.EnableGpuPreprocess = AskBool("启用GPU缩放到640 (失败自动回退)", cfg.EnableGpuPreprocess);
            cfg.EnablePboCapture = AskBool("启用PBO异步抓帧 (失败回退)", cfg.EnablePboCapture);

            cfg.EnableDynamicInterval = AskBool("启用动态检测间隔", cfg.EnableDynamicInterval);

            // 只有启用了动态才继续询问后面三个
            if (cfg.EnableDynamicInterval)
            {
                cfg.DynamicEmptyThreshold = AskInt("动态:连续无目标阈值", cfg.DynamicEmptyThreshold, 1, 2000);
                cfg.DynamicMaxInterval = AskInt("动态:最大提升间隔", cfg.DynamicMaxInterval, 1, 200);
                cfg.DynamicCooldownFrames = AskInt("动态:出现目标恢复缓冲帧", cfg.DynamicCooldownFrames, 1, 200);
            }
            else
            {
                Console.WriteLine("(已关闭动态间隔, 跳过动态参数设置)");
            }
            Console.WriteLine();
        }

        #region Helpers
        private static string GetCpuName()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                var v = k?.GetValue("ProcessorNameString") as string;
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            catch { }
            return "CPU";
        }
        private static int AskInt(string label, int cur, int min, int max)
        {
            while (true)
            {
                Console.Write($"{label} [{cur}] ({min}-{max}): ");
                var s = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(s)) return cur;
                if (int.TryParse(s, out int v) && v >= min && v <= max) return v;
                Console.WriteLine("无效数字");
            }
        }
        private static float AskFloat(string label, float cur, float min, float max)
        {
            while (true)
            {
                Console.Write($"{label} [{cur}] ({min}-{max}): ");
                var s = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(s)) return cur;
                if (float.TryParse(s, out float v) && v >= min && v <= max) return v;
                Console.WriteLine("无效数字");
            }
        }
        private static bool AskBool(string label, bool cur)
        {
            Console.Write($"{label} [{(cur ? "Y" : "N")}]: ");
            var s = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(s)) return cur;
            if (s.Equals("y", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("n", StringComparison.OrdinalIgnoreCase) || s.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            Console.WriteLine("输入无效，保持原值");
            return cur;
        }
        #endregion

        private static void DumpConfig(HookConfig c)
        {
            Console.WriteLine("\n最终配置:");
            Console.WriteLine($"  ModelPath             = {c.ModelPath}");
            Console.WriteLine($"  UseCPU                = {c.UseCPU}");
            Console.WriteLine($"  GpuDeviceId           = {c.GpuDeviceId}");
            Console.WriteLine($"  ForceDirectML         = {c.ForceDirectML}");
            Console.WriteLine($"  Verbose               = {c.Verbose}");
            Console.WriteLine($"  BaseDetectionInterval = {c.DetectionInterval}");
            Console.WriteLine($"  DynamicEnabled        = {c.EnableDynamicInterval}");
            if (c.EnableDynamicInterval)
            {
                Console.WriteLine($"  DynamicEmptyThreshold = {c.DynamicEmptyThreshold}");
                Console.WriteLine($"  DynamicMaxInterval    = {c.DynamicMaxInterval}");
                Console.WriteLine($"  DynamicCooldownFrames = {c.DynamicCooldownFrames}");
            }
            Console.WriteLine($"  ConfThreshold         = {c.ConfThreshold}");
            Console.WriteLine($"  NmsThreshold          = {c.NmsThreshold}");
            Console.WriteLine($"  DisableOverlay        = {c.DisableOverlay}");
            Console.WriteLine($"  ShowPerfMetrics       = {c.ShowPerfMetrics}");
            Console.WriteLine($"  GPU Preprocess        = {c.EnableGpuPreprocess}");
            Console.WriteLine($"  PBO Capture           = {c.EnablePboCapture}");
            Console.WriteLine();
        }

        #region Process / Path / Config
        private static int SelectPccProcess()
        {
            var list = new List<Process>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.IndexOf("pcc", StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(p);
                }
                catch { }
            }
            if (list.Count == 0)
            {
                Console.WriteLine("未找到包含 'pcc' 的进程。");
                return 0;
            }
            Console.WriteLine("候选进程:");
            for (int i = 0; i < list.Count; i++)
            {
                string path = "";
                try { path = list[i].MainModule?.FileName ?? ""; } catch { }
                Console.WriteLine($"{i}) PID={list[i].Id} Name={list[i].ProcessName} Path={path}");
            }
            if (list.Count == 1)
            {
                Console.WriteLine("自动选择唯一进程 PID=" + list[0].Id);
                return list[0].Id;
            }
            while (true)
            {
                Console.Write("输入序号: ");
                var s = Console.ReadLine();
                if (int.TryParse(s, out int idx) && idx >= 0 && idx < list.Count)
                    return list[idx].Id;
                Console.WriteLine("无效序号。");
            }
        }
        private static string ResolveHookLibPath()
        {
            string cur = AppDomain.CurrentDomain.BaseDirectory;
            string c1 = Path.Combine(cur, "HookLib.dll");
            if (File.Exists(c1)) return c1;
            string probe = Path.GetFullPath(Path.Combine(cur, @"..\..\HookLib\bin\x64\Debug\net48\HookLib.dll"));
            if (File.Exists(probe)) return probe;
            Console.Write("请输入 HookLib.dll 路径: ");
            var m = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(m) && File.Exists(m)) return m;
            return null;
        }
        private static HookConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(CONFIG_FILE)) return null;
                var cfg = new HookConfig();
                foreach (var line in File.ReadAllLines(CONFIG_FILE))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    var eq = t.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = t.Substring(0, eq).Trim();
                    var val = t.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "ModelPath": cfg.ModelPath = val == "" ? null : val; break;
                        case "DetectionInterval": if (int.TryParse(val, out var di)) cfg.DetectionInterval = di; break;
                        case "ConfThreshold": if (float.TryParse(val, out var cf)) cfg.ConfThreshold = cf; break;
                        case "NmsThreshold": if (float.TryParse(val, out var nm)) cfg.NmsThreshold = nm; break;
                        case "DisableOverlay": cfg.DisableOverlay = IsTrue(val); break;
                        case "LogDetailFrames": if (int.TryParse(val, out var lf)) cfg.LogDetailFrames = lf; break;
                        case "ForceDirectML": cfg.ForceDirectML = IsTrue(val); break;
                        case "UseCPU": cfg.UseCPU = IsTrue(val); break;
                        case "GpuDeviceId": if (int.TryParse(val, out var gid)) cfg.GpuDeviceId = gid; break;
                        case "Verbose": cfg.Verbose = IsTrue(val); break;
                        case "ShowPerfMetrics": cfg.ShowPerfMetrics = IsTrue(val); break;
                        case "EnableDynamicInterval": cfg.EnableDynamicInterval = IsTrue(val); break;
                        case "DynamicEmptyThreshold": if (int.TryParse(val, out var de)) cfg.DynamicEmptyThreshold = de; break;
                        case "DynamicMaxInterval": if (int.TryParse(val, out var dm)) cfg.DynamicMaxInterval = dm; break;
                        case "DynamicCooldownFrames": if (int.TryParse(val, out var dc)) cfg.DynamicCooldownFrames = dc; break;
                        case "EnableGpuPreprocess": cfg.EnableGpuPreprocess = IsTrue(val); break;
                        case "EnablePboCapture": cfg.EnablePboCapture = IsTrue(val); break;
                    }
                }
                return cfg;
            }
            catch { return null; }
        }
        private static void SaveConfig(HookConfig cfg)
        {
            try
            {
                var lines = new List<string>
                {
                    "# injector_config.txt",
                    "ModelPath=" + (cfg.ModelPath??""),
                    "DetectionInterval=" + cfg.DetectionInterval,
                    "ConfThreshold=" + cfg.ConfThreshold,
                    "NmsThreshold=" + cfg.NmsThreshold,
                    "DisableOverlay=" + (cfg.DisableOverlay?"1":"0"),
                    "LogDetailFrames=" + cfg.LogDetailFrames,
                    "ForceDirectML=" + (cfg.ForceDirectML?"1":"0"),
                    "UseCPU=" + (cfg.UseCPU?"1":"0"),
                    "GpuDeviceId=" + cfg.GpuDeviceId,
                    "Verbose=" + (cfg.Verbose?"1":"0"),
                    "ShowPerfMetrics=" + (cfg.ShowPerfMetrics?"1":"0"),
                    "EnableDynamicInterval=" + (cfg.EnableDynamicInterval?"1":"0"),
                    "DynamicEmptyThreshold=" + cfg.DynamicEmptyThreshold,
                    "DynamicMaxInterval=" + cfg.DynamicMaxInterval,
                    "DynamicCooldownFrames=" + cfg.DynamicCooldownFrames,
                    "EnableGpuPreprocess=" + (cfg.EnableGpuPreprocess?"1":"0"),
                    "EnablePboCapture=" + (cfg.EnablePboCapture?"1":"0")
                };
                File.WriteAllLines(CONFIG_FILE, lines);
            }
            catch (Exception ex) { Console.WriteLine("保存配置失败: " + ex.Message); }
        }
        private static bool IsTrue(string v)
            => v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        #endregion
    }
}