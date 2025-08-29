using System;

namespace HookLib
{
    [Serializable]
    public class HookConfig
    {
        public int DetectionInterval = 0;       // 允许 0 = 每帧推理
        public float ConfThreshold = 0.25f;
        public float NmsThreshold = 0.45f;

        public bool DisableOverlay = false;
        public int LogDetailFrames = 10;

        public string ModelPath;
        public bool ForceDirectML = true;
        public bool Verbose = false;
        public bool ShowPerfMetrics = true;

        // 设备选择
        public bool UseCPU = false;
        public int GpuDeviceId = -1;

        // 动态检测间隔
        public bool EnableDynamicInterval = true;
        public int DynamicEmptyThreshold = 30;   // 连续无目标帧数达到后提升间隔
        public int DynamicMaxInterval = 8;    // 动态提升最大有效间隔
        public int DynamicCooldownFrames = 5;    // 出现目标后恢复基础间隔前需多少帧稳定（可=1）

        // GPU 预处理开关（可禁用回退）
        public bool EnableGpuPreprocess = true;
        public bool EnablePboCapture = true;
    }
}