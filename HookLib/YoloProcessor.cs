using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
//using Microsoft.ML.OnnxRuntime.DirectML;

namespace HookLib
{
    internal static class YoloProcessor
    {
        private const int ModelSize = 640;

        // 复用缓冲区
        private static float[] _chw;
        private static readonly object _initLock = new object();
        private static InferenceSession _session;
        private static bool _failed;

        public static void EnsureInit()
        {
            if (_session != null || _failed) return;
            lock (_initLock)
            {
                if (_session != null || _failed) return;
                try
                {
                    bool useCPU = HookBootstrap.YoloRuntimeConfig.UseCPU;
                    bool wantDml = HookBootstrap.YoloRuntimeConfig.ForceDirectML && !useCPU;
                    int gpuId = HookBootstrap.YoloRuntimeConfig.GpuDeviceId;
                    bool verbose = HookBootstrap.YoloRuntimeConfig.Verbose;

                    string forced = HookBootstrap.YoloRuntimeConfig.ForcedModelPath;
                    string chosen = ResolveModelPath(forced, verbose);
                    if (chosen == null) { _failed = true; return; }

                    var opts = new SessionOptions
                    {
                        EnableMemoryPattern = true,
                        EnableCpuMemArena = true
                    };
                    // 最高图优化级别（老版本使用 ORT_ENABLE_ALL）
                    opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

                    // 线程设置（CPU下主意义）
                    opts.IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2);
                    opts.InterOpNumThreads = 1;

                    bool dmlUsed = false;
                    if (wantDml)
                    {
                        try
                        {
                            if (gpuId >= 0)
                                opts.AppendExecutionProvider_DML(gpuId);
                            else
                                opts.AppendExecutionProvider_DML();
                            dmlUsed = true;
                            Logging.Info("[YOLO] DirectML provider added " + (gpuId >= 0 ? ("device=" + gpuId) : "(auto)"));
                        }
                        catch (Exception ex)
                        {
                            Logging.Warn("[YOLO] DirectML init fail -> CPU : " + ex.Message);
                        }
                    }

                    try
                    {
                        _session = new InferenceSession(chosen, opts);
                        Logging.Info("[YOLO] 模型加载成功: " + chosen + (dmlUsed ? " (DirectML)" : " (CPU)"));
                    }
                    catch (Exception ex)
                    {
                        if (dmlUsed)
                        {
                            Logging.Warn("[YOLO] DML 会话创建失败回退 CPU: " + ex.Message);
                            try
                            {
                                _session = new InferenceSession(chosen, new SessionOptions
                                {
                                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                                });
                                Logging.Info("[YOLO] CPU 回退成功: " + chosen);
                            }
                            catch (Exception ex2)
                            {
                                Logging.Exception("[YOLO] CPU 回退仍失败", ex2);
                                _failed = true;
                            }
                        }
                        else
                        {
                            Logging.Exception("[YOLO] 创建失败", ex);
                            _failed = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Exception("[YOLO] 初始化异常", ex);
                    _failed = true;
                }
            }
        }

        private static string ResolveModelPath(string forced, bool verbose)
        {
            if (!string.IsNullOrWhiteSpace(forced) && File.Exists(forced))
                return forced;

            string env = Environment.GetEnvironmentVariable("YOLO_MODEL");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
                return env;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";
            string asmDir = Path.GetDirectoryName(typeof(YoloProcessor).Assembly.Location) ?? "";
            string curDir = Environment.CurrentDirectory ?? "";
            string procDir = "";
            try { using var p = Process.GetCurrentProcess(); procDir = Path.GetDirectoryName(p.MainModule?.FileName) ?? ""; } catch { }

            var dirs = new[] { baseDir, asmDir, curDir, procDir };
            string[] names = { "yolo.onnx", "model.onnx", "yolov5.onnx" };
            foreach (var d in dirs)
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                foreach (var n in names)
                {
                    var p1 = Path.Combine(d, n);
                    if (File.Exists(p1)) return p1;
                    var p2 = Path.Combine(d, "models", n);
                    if (File.Exists(p2)) return p2;
                }
            }

            Logging.Warn("[YOLO] 未找到模型文件。");
            if (verbose)
                Logging.Info("[YOLO] 搜索目录: " + string.Join(" ; ", dirs.Distinct()));
            return null;
        }

        public static List<Box> Detect(byte[] bgra, int frameW, int frameH)
        {
            EnsureInit();
            if (_session == null) return new List<Box>();
            try
            {
                if (_chw == null || _chw.Length < 3 * ModelSize * ModelSize)
                    _chw = new float[3 * ModelSize * ModelSize];

                bool alreadyResized = (frameW == ModelSize && frameH == ModelSize);
                float[] chw = _chw;
                int plane = ModelSize * ModelSize;

                // 填充 114/255
                float fill = 114f / 255f;
                for (int i = 0; i < 3 * plane; i++) chw[i] = fill;

                float scale = 1f;
                int resizedW = frameW;
                int resizedH = frameH;
                int padX = 0, padY = 0;

                if (!alreadyResized)
                {
                    scale = Math.Min(ModelSize / (float)frameW, ModelSize / (float)frameH);
                    resizedW = (int)Math.Round(frameW * scale);
                    resizedH = (int)Math.Round(frameH * scale);
                    padX = (ModelSize - resizedW) / 2;
                    padY = (ModelSize - resizedH) / 2;
                }
                else
                {
                    resizedW = ModelSize;
                    resizedH = ModelSize;
                    padX = padY = 0;
                }

                int[] mapX = null;
                int[] mapY = null;
                if (!alreadyResized)
                {
                    mapX = new int[frameW];
                    mapY = new int[frameH];
                    for (int x = 0; x < frameW; x++)
                        mapX[x] = Math.Min(resizedW - 1, (int)(x * scale));
                    for (int y = 0; y < frameH; y++)
                        mapY[y] = Math.Min(resizedH - 1, (int)(y * scale));
                }

                int srcStride = frameW * 4;
                if (alreadyResized)
                {
                    for (int y = 0; y < ModelSize; y++)
                    {
                        int rowOff = y * srcStride;
                        for (int x = 0; x < ModelSize; x++)
                        {
                            int si = rowOff + x * 4;
                            byte b = bgra[si + 0];
                            byte g = bgra[si + 1];
                            byte r = bgra[si + 2];
                            int baseIdx = y * ModelSize + x;
                            chw[0 * plane + baseIdx] = r / 255f;
                            chw[1 * plane + baseIdx] = g / 255f;
                            chw[2 * plane + baseIdx] = b / 255f;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < frameH; y++)
                    {
                        int srcRow = y * srcStride;
                        int mappedY = mapY[y] + padY;
                        for (int x = 0; x < frameW; x++)
                        {
                            int mappedX = mapX[x] + padX;
                            int si = srcRow + x * 4;
                            byte b = bgra[si + 0];
                            byte g = bgra[si + 1];
                            byte r = bgra[si + 2];
                            int baseIdx = mappedY * ModelSize + mappedX;
                            chw[0 * plane + baseIdx] = r / 255f;
                            chw[1 * plane + baseIdx] = g / 255f;
                            chw[2 * plane + baseIdx] = b / 255f;
                        }
                    }
                }

                var input = new DenseTensor<float>(chw, new[] { 1, 3, ModelSize, ModelSize });
                using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor("images", input) });
                var output = results.First().AsTensor<float>();
                var dims = output.Dimensions;
                if (dims.Length != 3) return new List<Box>();
                int nBoxes = dims[1];
                int stride = dims[2]; // 6
                if (stride != 6) return new List<Box>();

                float confTh = HookBootstrap.YoloRuntimeConfig.ConfThreshold;
                float nmsTh = HookBootstrap.YoloRuntimeConfig.NmsThreshold;
                float[] raw = output.ToArray();
                List<Box> boxes = new List<Box>();
                for (int i = 0; i < nBoxes; i++)
                {
                    int off = i * stride;
                    float cx = raw[off + 0];
                    float cy = raw[off + 1];
                    float w = raw[off + 2];
                    float h = raw[off + 3];
                    float obj = raw[off + 4];
                    float cls = raw[off + 5];
                    float score = obj * cls;
                    if (score < confTh) continue;

                    float x1 = cx - w / 2f;
                    float y1 = cy - h / 2f;
                    float x2 = cx + w / 2f;
                    float y2 = cy + h / 2f;

                    float invScale = alreadyResized ? 1f : 1f / scale;
                    x1 = (x1 - padX) * invScale;
                    x2 = (x2 - padX) * invScale;
                    y1 = (y1 - padY) * invScale;
                    y2 = (y2 - padY) * invScale;

                    x1 = Clamp(x1, 0, frameW - 1);
                    y1 = Clamp(y1, 0, frameH - 1);
                    x2 = Clamp(x2, 0, frameW - 1);
                    y2 = Clamp(y2, 0, frameH - 1);
                    if (x2 <= x1 + 1 || y2 <= y1 + 1) continue;
                    boxes.Add(new Box { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Score = score });
                }

                boxes.Sort((a, b) => b.Score.CompareTo(a.Score));
                bool[] removed = new bool[boxes.Count];
                List<Box> final = new List<Box>();
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (removed[i]) continue;
                    var bi = boxes[i];
                    final.Add(bi);
                    for (int j = i + 1; j < boxes.Count; j++)
                    {
                        if (removed[j]) continue;
                        if (IoU(bi, boxes[j]) > nmsTh)
                            removed[j] = true;
                    }
                }
                return final;
            }
            catch (Exception ex)
            {
                Logging.Exception("[YOLO] Detect异常", ex);
                return new List<Box>();
            }
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        private static float IoU(Box a, Box b)
        {
            float xx1 = Math.Max(a.X1, b.X1);
            float yy1 = Math.Max(a.Y1, b.Y1);
            float xx2 = Math.Min(a.X2, b.X2);
            float yy2 = Math.Min(a.Y2, b.Y2);
            float w = Math.Max(0, xx2 - xx1);
            float h = Math.Max(0, yy2 - yy1);
            float inter = w * h;
            float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
            float union = areaA + areaB - inter + 1e-6f;
            return inter / union;
        }
    }
}