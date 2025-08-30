using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using EasyHook;

namespace HookLib
{
    internal static class HookBootstrap
    {
        private static HookConfig _externalConfig;
        public static void ApplyExternalConfig(HookConfig cfg)
        {
            _externalConfig = cfg;
            Logging.Info("[Config] External: " +
                         $"BaseInt={cfg.DetectionInterval},Dyn={cfg.EnableDynamicInterval},DynMax={cfg.DynamicMaxInterval}," +
                         $"DynEmptyTh={cfg.DynamicEmptyThreshold},DynCool={cfg.DynamicCooldownFrames}," +
                         $"GPUPre={cfg.EnableGpuPreprocess},PBO={cfg.EnablePboCapture}," +
                         $"Conf={cfg.ConfThreshold},NMS={cfg.NmsThreshold},ForceDML={cfg.ForceDirectML},UseCPU={cfg.UseCPU},GpuId={cfg.GpuDeviceId}");
            YoloRuntimeConfig.ConfThreshold = cfg.ConfThreshold;
            YoloRuntimeConfig.NmsThreshold = cfg.NmsThreshold;
            YoloRuntimeConfig.ForcedModelPath = cfg.ModelPath;
            YoloRuntimeConfig.Verbose = cfg.Verbose;
            YoloRuntimeConfig.ForceDirectML = cfg.ForceDirectML;
            YoloRuntimeConfig.UseCPU = cfg.UseCPU;
            YoloRuntimeConfig.GpuDeviceId = cfg.GpuDeviceId;
        }
        internal static class YoloRuntimeConfig
        {
            public static float ConfThreshold = 0.25f;
            public static float NmsThreshold = 0.45f;
            public static string ForcedModelPath;
            public static bool Verbose;
            public static bool ForceDirectML = true;
            public static bool UseCPU = false;
            public static int GpuDeviceId = -1;
        }

        #region GL Delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr wglCreateContextDelegate(IntPtr hdc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate bool wglMakeCurrentDelegate(IntPtr hdc, IntPtr hglrc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr wglGetCurrentContextDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr wglGetCurrentDCDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate bool SwapBuffersDelegate(IntPtr hdc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glGetIntegervDelegate(int pname, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glReadPixelsDelegate(int x, int y, int w, int h, uint format, uint type, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glGenBuffersDelegate(int n, IntPtr buffers);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBindBufferDelegate(uint target, uint buffer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBufferDataDelegate(uint target, IntPtr size, IntPtr data, uint usage);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr glMapBufferDelegate(uint target, uint access);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate bool glUnmapBufferDelegate(uint target);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glDeleteBuffersDelegate(int n, IntPtr buffers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glGenFramebuffersDelegate(int n, IntPtr ids);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBindFramebufferDelegate(uint target, uint id);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glFramebufferTexture2DDelegate(uint target, uint attachment, uint textarget, uint texture, int level);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glDeleteFramebuffersDelegate(int n, IntPtr ids);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glGenTexturesDelegate(int n, IntPtr textures);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBindTextureDelegate(uint target, uint tex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glTexParameteriDelegate(uint target, uint pname, int param);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glTexImage2DDelegate(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBlitFramebufferDelegate(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, uint mask, uint filter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glMatrixModeDelegate(uint mode);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glPushMatrixDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glPopMatrixDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glLoadIdentityDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glOrthoDelegate(double l, double r, double b, double t, double n, double f);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBeginDelegate(uint mode);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glEndDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glVertex2fDelegate(float x, float y);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glColor4fDelegate(float r, float g, float b, float a);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glLineWidthDelegate(float w);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glDisableDelegate(uint cap);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glEnableDelegate(uint cap);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glPushAttribDelegate(uint mask);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glPopAttribDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glBlendFuncDelegate(uint sfactor, uint dfactor);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void glTexCoord2fDelegate(float s, float t);
        #endregion

        #region Hooks
        private static LocalHook _hookCreateCtx;
        private static LocalHook _hookMakeCurrent;
        private static LocalHook _hookSwapBuffers;
        private static wglCreateContextDelegate _origCreateCtx;
        private static wglMakeCurrentDelegate _origMakeCurrent;
        private static SwapBuffersDelegate _origSwapBuffers;
        private static wglGetCurrentContextDelegate _wglGetCurrentContext;
        private static wglGetCurrentDCDelegate _wglGetCurrentDC;
        #endregion

        #region GL Functions
        private static glGetIntegervDelegate _glGetIntegerv;
        private static glReadPixelsDelegate _glReadPixels;

        private static glGenBuffersDelegate _glGenBuffers;
        private static glBindBufferDelegate _glBindBuffer;
        private static glBufferDataDelegate _glBufferData;
        private static glMapBufferDelegate _glMapBuffer;
        private static glUnmapBufferDelegate _glUnmapBuffer;
        private static glDeleteBuffersDelegate _glDeleteBuffers;

        private static glGenFramebuffersDelegate _glGenFramebuffers;
        private static glBindFramebufferDelegate _glBindFramebuffer;
        private static glFramebufferTexture2DDelegate _glFramebufferTexture2D;
        private static glDeleteFramebuffersDelegate _glDeleteFramebuffers;
        private static glGenTexturesDelegate _glGenTextures;
        private static glBindTextureDelegate _glBindTexture;
        private static glTexParameteriDelegate _glTexParameteri;
        private static glTexImage2DDelegate _glTexImage2D;
        private static glBlitFramebufferDelegate _glBlitFramebuffer;

        private static glMatrixModeDelegate _glMatrixMode;
        private static glPushMatrixDelegate _glPushMatrix;
        private static glPopMatrixDelegate _glPopMatrix;
        private static glLoadIdentityDelegate _glLoadIdentity;
        private static glOrthoDelegate _glOrtho;
        private static glBeginDelegate _glBegin;
        private static glEndDelegate _glEnd;
        private static glVertex2fDelegate _glVertex2f;
        private static glColor4fDelegate _glColor4f;
        private static glLineWidthDelegate _glLineWidth;
        private static glDisableDelegate _glDisable;
        private static glEnableDelegate _glEnable;
        private static glPushAttribDelegate _glPushAttrib;
        private static glPopAttribDelegate _glPopAttrib;
        private static glBlendFuncDelegate _glBlendFunc;
        private static glTexCoord2fDelegate _glTexCoord2f;
        #endregion

        #region State
        private static bool _started;
        private static IntPtr _currentCtx = IntPtr.Zero;
        private static IntPtr _currentDC = IntPtr.Zero;
        [ThreadStatic] private static bool _inSwap;
        private static bool _coreLoaded;
        private static bool _drawLoaded;

        private static int _frameCount;
        private static int _baseDetectionInterval = 0;
        private static bool _dynamicEnabled = true;
        private static int _dynamicEmptyThreshold = 30;
        private static int _dynamicMaxInterval = 8;
        private static int _dynamicCooldownFrames = 5;
        private static int _effectiveDetectionInterval = 0;
        private static int _emptyStreak;
        private static int _cooldownStreak;

        private static bool _disableOverlay;
        private static bool _showPerfMetrics;
        private static bool _enableGpuPreprocess = true;
        private static bool _enablePbo = true;

        private static FrameRing _frameRing = new FrameRing(3);
        private static Thread _detThread;
        private static volatile bool _detStop;
        private static readonly object _boxesLock = new object();
        private static List<Box> _latestBoxes = new List<Box>();

        private static byte[] _scratchFull;
        private static byte[] _scratchScaled;
        private static byte[] _scratchDetect;
        private static byte[] _pboStage;

        // 性能
        private static long _lastFrameSwapTicks;
        private static double _fpsSmooth;
        private static double _captureMs;
        private static double _overlayMs;
        private static volatile float _lastDetectMs;
        private static volatile int _lastDetectFrameIdx;
        private static long _injectStartTicks = Stopwatch.GetTimestamp();

        // 检测 FPS 统计
        private static readonly object _detTimesLock = new object();
        private static readonly Queue<long> _detTimes = new Queue<long>();
        private static double _detFps; // 一秒窗口内检测完成次数
        private const double DET_WINDOW_MS = 1000.0;

        // 字体
        private static bool _fontReady;
        private static uint _fontTexId;
        private const int FONT_CELL_W = 8;
        private const int FONT_CELL_H = 8;
        private const int FONT_COLS = 16;
        private const int FONT_ROWS = 6;

        // PBO
        private static bool _pboReady;
        private static uint _pboA, _pboB;
        private static int _pboIndex;
        private static bool _pboFailed;

        // FBO
        private static bool _fboReady;
        private static uint _fboId;
        private static uint _fboTex;
        private static bool _fboFailed;

        private static int _lastDetectFrame = -1000;
        #endregion

        #region GL Constants
        private const int GL_VIEWPORT = 0x0BA2;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_BGRA = 0x80E1;
        private const uint GL_UNSIGNED_BYTE = 0x1401;

        private const uint GL_PIXEL_PACK_BUFFER = 0x88EB;
        private const uint GL_STREAM_READ = 0x88E1;
        private const uint GL_READ_ONLY = 0x88B8;

        private const uint GL_FRAMEBUFFER = 0x8D40;
        private const uint GL_READ_FRAMEBUFFER = 0x8CA8;
        private const uint GL_DRAW_FRAMEBUFFER = 0x8CA9;
        private const uint GL_COLOR_ATTACHMENT0 = 0x8CE0;
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_LINEAR = 0x2601;
        private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        private const uint GL_TEXTURE_MAG_FILTER = 0x2800;

        private const uint GL_PROJECTION = 0x1701;
        private const uint GL_MODELVIEW = 0x1700;
        private const uint GL_LINE_LOOP = 0x0002;
        private const uint GL_DEPTH_TEST = 0x0B71;
        private const uint GL_ALL_ATTRIB_BITS = 0xFFFFFFFF;
        private const uint GL_QUADS = 0x0007;
        private const uint GL_BLEND = 0x0BE2;
        private const uint GL_SRC_ALPHA = 0x0302;
        private const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        private const uint GL_COLOR_BUFFER_BIT = 0x4000;
        private const uint GL_NEAREST = 0x2600;
        #endregion

        public static void Start()
        {
            if (_started) return;
            _started = true;
            ReadEnvConfig();
            OverrideWithExternal();

            try
            {
                Logging.Info("[Bootstrap] Start hooking");
                IntPtr hOGL = EnsureModule("opengl32.dll");
                IntPtr hGDI = EnsureModule("gdi32.dll");
                if (hOGL == IntPtr.Zero || hGDI == IntPtr.Zero)
                {
                    Logging.Warn("[Bootstrap] opengl32 / gdi32 加载失败。");
                    return;
                }
                LoadWglAccessors(hOGL);
                HookExport(hOGL, "wglCreateContext", p => {
                    _origCreateCtx = Marshal.GetDelegateForFunctionPointer<wglCreateContextDelegate>(p);
                    _hookCreateCtx = LocalHook.Create(p, new wglCreateContextDelegate(WglCreateContextHook), null);
                    _hookCreateCtx.ThreadACL.SetExclusiveACL(new int[0]);
                    Logging.Info("[Hook] wglCreateContext");
                });
                HookExport(hOGL, "wglMakeCurrent", p => {
                    _origMakeCurrent = Marshal.GetDelegateForFunctionPointer<wglMakeCurrentDelegate>(p);
                    _hookMakeCurrent = LocalHook.Create(p, new wglMakeCurrentDelegate(WglMakeCurrentHook), null);
                    _hookMakeCurrent.ThreadACL.SetExclusiveACL(new int[0]);
                    Logging.Info("[Hook] wglMakeCurrent");
                });
                HookExport(hGDI, "SwapBuffers", p => {
                    _origSwapBuffers = Marshal.GetDelegateForFunctionPointer<SwapBuffersDelegate>(p);
                    _hookSwapBuffers = LocalHook.Create(p, new SwapBuffersDelegate(SwapBuffersHook), null);
                    _hookSwapBuffers.ThreadACL.SetExclusiveACL(new int[0]);
                    Logging.Info("[Hook] SwapBuffers");
                });

                StartDetectionThread();
                Logging.Info($"[Bootstrap] BaseInterval={_baseDetectionInterval} Dynamic={_dynamicEnabled} MaxDyn={_dynamicMaxInterval}");
            }
            catch (Exception ex)
            {
                Logging.Exception("Bootstrap.Start", ex);
            }
        }

        private static void LoadWglAccessors(IntPtr hOGL)
        {
            try
            {
                IntPtr pCtx = NativeMethods.GetProcAddress(hOGL, "wglGetCurrentContext");
                IntPtr pDC = NativeMethods.GetProcAddress(hOGL, "wglGetCurrentDC");
                if (pCtx != IntPtr.Zero)
                    _wglGetCurrentContext = Marshal.GetDelegateForFunctionPointer<wglGetCurrentContextDelegate>(pCtx);
                if (pDC != IntPtr.Zero)
                    _wglGetCurrentDC = Marshal.GetDelegateForFunctionPointer<wglGetCurrentDCDelegate>(pDC);
                Logging.Info("[WGL] GetCurrentContext=" + (pCtx != IntPtr.Zero) + " GetCurrentDC=" + (pDC != IntPtr.Zero));
            }
            catch (Exception ex)
            {
                Logging.Exception("LoadWglAccessors", ex);
            }
        }
        private static void ReadEnvConfig() { }
        private static void OverrideWithExternal()
        {
            if (_externalConfig == null) return;
            _baseDetectionInterval = _externalConfig.DetectionInterval;
            _effectiveDetectionInterval = _baseDetectionInterval;
            _disableOverlay = _externalConfig.DisableOverlay;
            _showPerfMetrics = _externalConfig.ShowPerfMetrics;
            _dynamicEnabled = _externalConfig.EnableDynamicInterval;
            _dynamicEmptyThreshold = _externalConfig.DynamicEmptyThreshold;
            _dynamicMaxInterval = _externalConfig.DynamicMaxInterval;
            _dynamicCooldownFrames = _externalConfig.DynamicCooldownFrames;
            _enableGpuPreprocess = _externalConfig.EnableGpuPreprocess;
            _enablePbo = _externalConfig.EnablePboCapture;
        }

        #region Detection Thread
        private static void StartDetectionThread()
        {
            _detThread = new Thread(DetectionWorker) { IsBackground = true, Name = "YOLO-Detection" };
            _detThread.Start();
        }
        private static void DetectionWorker()
        {
            int lastProcessed = -1;
            while (!_detStop)
            {
                try
                {
                    var f = _frameRing.TryGetLatest(lastProcessed);
                    if (f == null) { Thread.Sleep(1); continue; }
                    lastProcessed = f.FrameIndex;

                    int eff = Volatile.Read(ref _effectiveDetectionInterval);
                    if (eff > 0 && f.FrameIndex - Volatile.Read(ref _lastDetectFrame) < eff)
                        continue;

                    var sw = Stopwatch.StartNew();
                    var boxes = YoloProcessor.Detect(f.Pixels, f.Width, f.Height) ?? new List<Box>();
                    sw.Stop();

                    _lastDetectMs = (float)sw.Elapsed.TotalMilliseconds;
                    _lastDetectFrameIdx = f.FrameIndex;
                    Volatile.Write(ref _lastDetectFrame, f.FrameIndex);
                    lock (_boxesLock) _latestBoxes = boxes;

                    UpdateDynamicInterval(boxes.Count);
                    UpdateDetectionFps(); // 更新检测 FPS

                    if (f.FrameIndex < _externalConfig.LogDetailFrames)
                        Logging.Info($"[Detect] Frame={f.FrameIndex} Boxes={boxes.Count} DetMs={_lastDetectMs:F2} EffInt={_effectiveDetectionInterval}");
                }
                catch (Exception ex)
                {
                    Logging.Exception("[DetectThread]", ex);
                    Thread.Sleep(10);
                }
            }
        }

        // 统计 1 秒窗口内完成检测次数 => _detFps
        private static void UpdateDetectionFps()
        {
            long now = Stopwatch.GetTimestamp();
            lock (_detTimesLock)
            {
                _detTimes.Enqueue(now);
                double windowTicks = DET_WINDOW_MS * Stopwatch.Frequency / 1000.0;
                while (_detTimes.Count > 0 && (now - _detTimes.Peek()) > windowTicks)
                    _detTimes.Dequeue();

                if (_detTimes.Count >= 2)
                {
                    long first = _detTimes.Peek();
                    long last = 0;
                    foreach (var t in _detTimes) last = t;
                    double spanMs = (last - first) * 1000.0 / Stopwatch.Frequency;
                    if (spanMs > 0.5) // 至少覆盖 0.5ms 避免除零
                        _detFps = (_detTimes.Count - 1) * 1000.0 / spanMs;
                    else
                        _detFps = 0;
                }
                else
                {
                    _detFps = 0;
                }
            }
        }

        private static void UpdateDynamicInterval(int boxCount)
        {
            if (!_dynamicEnabled) return;
            if (boxCount == 0)
            {
                _emptyStreak++;
                _cooldownStreak = 0;
                if (_emptyStreak >= _dynamicEmptyThreshold)
                {
                    int target = Math.Min(_dynamicMaxInterval, Math.Max(_baseDetectionInterval, _effectiveDetectionInterval + 1));
                    if (target != _effectiveDetectionInterval)
                        _effectiveDetectionInterval = target;
                }
            }
            else
            {
                _emptyStreak = 0;
                _cooldownStreak++;
                if (_effectiveDetectionInterval != _baseDetectionInterval &&
                    _cooldownStreak >= _dynamicCooldownFrames)
                {
                    _effectiveDetectionInterval = _baseDetectionInterval;
                }
            }
        }
        #endregion

        #region Hooks
        private static IntPtr WglCreateContextHook(IntPtr hdc)
        {
            IntPtr ctx = _origCreateCtx(hdc);
            return ctx;
        }
        private static bool WglMakeCurrentHook(IntPtr hdc, IntPtr hglrc)
        {
            bool ok = _origMakeCurrent(hdc, hglrc);
            if (ok)
            {
                _currentCtx = hglrc;
                _currentDC = hdc;
            }
            return ok;
        }
        private static bool SwapBuffersHook(IntPtr hdc)
        {
            if (_inSwap) return _origSwapBuffers(hdc);
            _inSwap = true;
            long frameStartTicks = Stopwatch.GetTimestamp();
            try
            {
                if (_currentCtx == IntPtr.Zero && _wglGetCurrentContext != null)
                {
                    var c = _wglGetCurrentContext();
                    if (c != IntPtr.Zero)
                    {
                        _currentCtx = c;
                        _currentDC = _wglGetCurrentDC?.Invoke() ?? hdc;
                    }
                }

                if (_currentCtx != IntPtr.Zero)
                {
                    EnsureCoreGL();
                    if (_coreLoaded)
                    {
                        var capStart = Stopwatch.GetTimestamp();
                        if (CaptureFrame(out int w, out int h, out byte[] bgra640, out bool alreadyResized))
                        {
                            var capEnd = Stopwatch.GetTimestamp();
                            _captureMs = (capEnd - capStart) * 1000.0 / Stopwatch.Frequency;

                            int frameIndex = _frameCount;
                            var slot = _frameRing.GetSlot(frameIndex);
                            slot.EnsureSize(w, h);
                            Buffer.BlockCopy(bgra640, 0, slot.Pixels, 0, w * h * 4);
                            slot.Width = w; slot.Height = h; slot.FrameIndex = frameIndex;
                            slot.TimestampTicks = DateTime.UtcNow.Ticks;
                            _frameRing.Publish(frameIndex);

                            if (!_disableOverlay)
                            {
                                List<Box> boxesCopy;
                                lock (_boxesLock) boxesCopy = _latestBoxes;
                                EnsureOverlayGL();
                                if (_drawLoaded)
                                {
                                    var ovStart = Stopwatch.GetTimestamp();
                                    DrawBoxesAndMetrics(w, h, boxesCopy);
                                    var ovEnd = Stopwatch.GetTimestamp();
                                    _overlayMs = (ovEnd - ovStart) * 1000.0 / Stopwatch.Frequency;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Exception("SwapBuffersHook", ex);
            }
            finally
            {
                if (_lastFrameSwapTicks != 0)
                {
                    double dtMs = (Stopwatch.GetTimestamp() - _lastFrameSwapTicks) * 1000.0 / Stopwatch.Frequency;
                    double instFps = dtMs > 0 ? 1000.0 / dtMs : 0;
                    _fpsSmooth = _fpsSmooth == 0 ? instFps : (_fpsSmooth * 0.9 + instFps * 0.1);
                }
                _lastFrameSwapTicks = frameStartTicks;
                _frameCount++;
                _inSwap = false;
            }
            return _origSwapBuffers(hdc);
        }
        #endregion

        #region Capture + PBO + FBO
        private static bool CaptureFrame(out int w, out int h, out byte[] bgraOut, out bool alreadyResized)
        {
            w = h = 0;
            bgraOut = null;
            alreadyResized = false;
            try
            {
                int[] vp = new int[4];
                unsafe
                {
                    fixed (int* pvp = vp)
                        _glGetIntegerv(GL_VIEWPORT, (IntPtr)pvp);
                }
                int vw = vp[2];
                int vh = vp[3];
                if (vw <= 0 || vh <= 0 || vw > 20000 || vh > 20000) return false;

                bool useGpuScale = _enableGpuPreprocess && !_fboFailed && EnsureFboForScale();
                if (useGpuScale)
                {
                    try
                    {
                        _glBindFramebuffer(GL_READ_FRAMEBUFFER, 0);
                        _glBindFramebuffer(GL_DRAW_FRAMEBUFFER, _fboId);
                        _glBlitFramebuffer(0, 0, vw, vh, 0, 0, 640, 640, GL_COLOR_BUFFER_BIT, GL_LINEAR);
                        _glBindFramebuffer(GL_READ_FRAMEBUFFER, _fboId);
                        w = 640; h = 640;
                        if (_scratchScaled == null || _scratchScaled.Length != 640 * 640 * 4)
                            _scratchScaled = new byte[640 * 640 * 4];
                        if (!ReadPixelsToBuffer(w, h, ref _scratchScaled))
                            throw new Exception("FBO glReadPixels 失败");
                        bgraOut = _scratchScaled;
                        alreadyResized = true;
                        _glBindFramebuffer(GL_READ_FRAMEBUFFER, 0);
                        _glBindFramebuffer(GL_DRAW_FRAMEBUFFER, 0);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logging.Warn("[Capture] GPU 预处理失败回退: " + ex.Message);
                        _fboFailed = true;
                    }
                }

                if (_scratchFull == null || _scratchFull.Length != vw * vh * 4)
                    _scratchFull = new byte[vw * vh * 4];
                if (!ReadPixelsToBuffer(vw, vh, ref _scratchFull))
                    return false;

                if (_scratchDetect == null || _scratchDetect.Length != vw * vh * 4)
                    _scratchDetect = new byte[vw * vh * 4];

                int stride = vw * 4;
                for (int row = 0; row < vh; row++)
                {
                    int srcOff = row * stride;
                    int dstRow = vh - 1 - row;
                    Buffer.BlockCopy(_scratchFull, srcOff, _scratchDetect, dstRow * stride, stride);
                }

                w = vw; h = vh;
                bgraOut = _scratchDetect;
                return true;
            }
            catch (Exception ex)
            {
                Logging.Exception("CaptureFrame", ex);
                return false;
            }
        }

        private static bool ReadPixelsToBuffer(int w, int h, ref byte[] buffer)
        {
            int bytes = w * h * 4;
            if (buffer == null || buffer.Length != bytes)
                buffer = new byte[bytes];

            if (_enablePbo && !_pboFailed && EnsurePbo(bytes))
            {
                try
                {
                    uint cur = (_pboIndex == 0) ? _pboA : _pboB;
                    uint prev = (_pboIndex == 0) ? _pboB : _pboA;

                    _glBindBuffer(GL_PIXEL_PACK_BUFFER, cur);
                    _glReadPixels(0, 0, w, h, GL_BGRA, GL_UNSIGNED_BYTE, IntPtr.Zero);

                    _glBindBuffer(GL_PIXEL_PACK_BUFFER, prev);
                    IntPtr ptr = _glMapBuffer(GL_PIXEL_PACK_BUFFER, GL_READ_ONLY);
                    if (ptr != IntPtr.Zero)
                    {
                        if (_pboStage == null || _pboStage.Length != bytes)
                            _pboStage = new byte[bytes];
                        Marshal.Copy(ptr, _pboStage, 0, Math.Min(_pboStage.Length, bytes));
                        _glUnmapBuffer(GL_PIXEL_PACK_BUFFER);
                        Buffer.BlockCopy(_pboStage, 0, buffer, 0, bytes);
                    }
                    _glBindBuffer(GL_PIXEL_PACK_BUFFER, 0);
                    _pboIndex = 1 - _pboIndex;
                    return true;
                }
                catch (Exception ex)
                {
                    Logging.Warn("[PBO] 失败回退: " + ex.Message);
                    _pboFailed = true;
                }
            }

            unsafe
            {
                fixed (byte* p = buffer)
                    _glReadPixels(0, 0, w, h, GL_BGRA, GL_UNSIGNED_BYTE, (IntPtr)p);
            }
            return true;
        }

        private static bool EnsurePbo(int bytes)
        {
            if (_pboFailed) return false;
            if (_pboReady) return true;
            if (_glGenBuffers == null || _glBindBuffer == null || _glBufferData == null)
                return false;
            try
            {
                unsafe
                {
                    var tmp = stackalloc uint[2];
                    _glGenBuffers(2, (IntPtr)tmp);
                    _pboA = tmp[0];
                    _pboB = tmp[1];
                }
                _glBindBuffer(GL_PIXEL_PACK_BUFFER, _pboA);
                _glBufferData(GL_PIXEL_PACK_BUFFER, (IntPtr)bytes, IntPtr.Zero, GL_STREAM_READ);
                _glBindBuffer(GL_PIXEL_PACK_BUFFER, _pboB);
                _glBufferData(GL_PIXEL_PACK_BUFFER, (IntPtr)bytes, IntPtr.Zero, GL_STREAM_READ);
                _glBindBuffer(GL_PIXEL_PACK_BUFFER, 0);
                _pboReady = true;
                Logging.Info("[PBO] 初始化成功");
            }
            catch (Exception ex)
            {
                Logging.Warn("[PBO] 初始化失败: " + ex.Message);
                _pboFailed = true;
            }
            return _pboReady;
        }

        private static bool EnsureFboForScale()
        {
            if (_fboFailed) return false;
            if (_fboReady) return true;
            if (_glGenFramebuffers == null || _glBindFramebuffer == null || _glGenTextures == null || _glTexImage2D == null || _glFramebufferTexture2D == null || _glBlitFramebuffer == null)
                return false;
            try
            {
                unsafe
                {
                    var p = stackalloc uint[1];
                    _glGenFramebuffers(1, (IntPtr)p);
                    _fboId = p[0];
                    _glGenTextures(1, (IntPtr)p);
                    _fboTex = p[0];
                }
                _glBindTexture(GL_TEXTURE_2D, _fboTex);
                _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                _glTexImage2D(GL_TEXTURE_2D, 0, 4, 640, 640, 0, GL_BGRA, GL_UNSIGNED_BYTE, IntPtr.Zero);

                _glBindFramebuffer(GL_FRAMEBUFFER, _fboId);
                _glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, _fboTex, 0);
                _glBindFramebuffer(GL_FRAMEBUFFER, 0);
                _fboReady = true;
                Logging.Info("[FBO] 640x640 初始化成功");
            }
            catch (Exception ex)
            {
                Logging.Warn("[FBO] 初始化失败: " + ex.Message);
                _fboFailed = true;
            }
            return _fboReady;
        }
        #endregion

        #region Overlay
        private static void EnsureOverlayGL()
        {
            if (_drawLoaded) return;
            try
            {
                IntPtr h = NativeMethods.GetModuleHandle("opengl32.dll");
                if (h == IntPtr.Zero) return;
                T L<T>(string n) where T : class
                {
                    IntPtr p = NativeMethods.GetProcAddress(h, n);
                    if (p == IntPtr.Zero) return null;
                    return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
                }
                _glMatrixMode = L<glMatrixModeDelegate>("glMatrixMode");
                _glPushMatrix = L<glPushMatrixDelegate>("glPushMatrix");
                _glPopMatrix = L<glPopMatrixDelegate>("glPopMatrix");
                _glLoadIdentity = L<glLoadIdentityDelegate>("glLoadIdentity");
                _glOrtho = L<glOrthoDelegate>("glOrtho");
                _glBegin = L<glBeginDelegate>("glBegin");
                _glEnd = L<glEndDelegate>("glEnd");
                _glVertex2f = L<glVertex2fDelegate>("glVertex2f");
                _glColor4f = L<glColor4fDelegate>("glColor4f");
                _glLineWidth = L<glLineWidthDelegate>("glLineWidth");
                _glDisable = L<glDisableDelegate>("glDisable");
                _glEnable = L<glEnableDelegate>("glEnable");
                _glPushAttrib = L<glPushAttribDelegate>("glPushAttrib");
                _glPopAttrib = L<glPopAttribDelegate>("glPopAttrib");
                _glGenTextures = L<glGenTexturesDelegate>("glGenTextures");
                _glBindTexture = L<glBindTextureDelegate>("glBindTexture");
                _glTexParameteri = L<glTexParameteriDelegate>("glTexParameteri");
                _glTexImage2D = L<glTexImage2DDelegate>("glTexImage2D");
                _glBlendFunc = L<glBlendFuncDelegate>("glBlendFunc");
                _glTexCoord2f = L<glTexCoord2fDelegate>("glTexCoord2f");

                _drawLoaded = _glBegin != null && _glVertex2f != null;
                if (_drawLoaded) Logging.Info("[Overlay] 函数加载完成");
            }
            catch (Exception ex) { Logging.Exception("EnsureOverlayGL", ex); }
        }

        private static void DrawBoxesAndMetrics(int w, int h, List<Box> boxes)
        {
            try
            {
                _glPushAttrib?.Invoke(GL_ALL_ATTRIB_BITS);
                _glDisable?.Invoke(GL_DEPTH_TEST);
                _glMatrixMode(GL_PROJECTION); _glPushMatrix(); _glLoadIdentity(); _glOrtho(0, w, h, 0, -1, 1);
                _glMatrixMode(GL_MODELVIEW); _glPushMatrix(); _glLoadIdentity();

                if (boxes != null && boxes.Count > 0)
                {
                    _glLineWidth?.Invoke(2f);
                    _glColor4f?.Invoke(0f, 1f, 0f, 1f);
                    foreach (var b in boxes)
                    {
                        _glBegin(GL_LINE_LOOP);
                        _glVertex2f(b.X1, b.Y1);
                        _glVertex2f(b.X2, b.Y1);
                        _glVertex2f(b.X2, b.Y2);
                        _glVertex2f(b.X1, b.Y2);
                        _glEnd();
                    }
                }

                if (_showPerfMetrics)
                {
                    EnsureFontTexture();
                    _glEnable?.Invoke(GL_TEXTURE_2D);
                    _glEnable?.Invoke(GL_BLEND);
                    _glBlendFunc?.Invoke(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
                    _glColor4f?.Invoke(0f, 1f, 0f, 1f);

                    long now = Stopwatch.GetTimestamp();
                    double runtimeSec = (now - _injectStartTicks) / (double)Stopwatch.Frequency;
                    string timeStr = FormatRuntime(runtimeSec);
                    double detFpsSnapshot;
                    lock (_detTimesLock) detFpsSnapshot = _detFps;

                    // 替换原来的 MAX：现在显示一秒检测次数 DET
                    // 行内容：FPS(渲染) / TIME / DET(检测FPS) / INT(检测间隔)
                    string line = $"FPS:{_fpsSmooth:0.0} TIME:{timeStr} DET:{detFpsSnapshot:0.0} INT:{_effectiveDetectionInterval}";
                    DrawText(6, 14, line);

                    _glDisable?.Invoke(GL_TEXTURE_2D);
                    _glDisable?.Invoke(GL_BLEND);
                }

                _glMatrixMode(GL_MODELVIEW); _glPopMatrix();
                _glMatrixMode(GL_PROJECTION); _glPopMatrix();
                _glPopAttrib?.Invoke();
            }
            catch (Exception ex) { Logging.Exception("DrawBoxesAndMetrics", ex); }
        }
        #endregion

        #region Font
        private static void EnsureFontTexture()
        {
            if (_fontReady) return;
            if (_glGenTextures == null || _glBindTexture == null || _glTexImage2D == null) return;
            int atlasW = FONT_COLS * FONT_CELL_W;
            int atlasH = FONT_ROWS * FONT_CELL_H;
            byte[] rgba = new byte[atlasW * atlasH * 4];
            for (int c = 32; c < 128; c++)
            {
                int ci = c - 32;
                int col = ci % FONT_COLS;
                int row = ci / FONT_COLS;
                int ox = col * FONT_CELL_W;
                int oy = row * FONT_CELL_H;
                byte[] g = GetGlyph8x8((char)c);
                for (int gy = 0; gy < 8; gy++)
                {
                    byte line = g[gy];
                    for (int gx = 0; gx < 8; gx++)
                    {
                        bool on = ((line >> (7 - gx)) & 1) != 0;
                        int px = ox + gx;
                        int py = oy + gy;
                        int off = (py * atlasW + px) * 4;
                        rgba[off + 0] = 255;
                        rgba[off + 1] = 255;
                        rgba[off + 2] = 255;
                        rgba[off + 3] = (byte)(on ? 255 : 0);
                    }
                }
            }
            unsafe
            {
                uint tex = 0;
                var tmp = Marshal.AllocHGlobal(sizeof(uint));
                try
                {
                    _glGenTextures(1, tmp);
                    tex = (uint)Marshal.ReadInt32(tmp);
                }
                finally { Marshal.FreeHGlobal(tmp); }
                _fontTexId = tex;
                _glBindTexture(GL_TEXTURE_2D, tex);
                _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                fixed (byte* p = rgba)
                    _glTexImage2D(GL_TEXTURE_2D, 0, 4, atlasW, atlasH, 0, GL_RGBA, GL_UNSIGNED_BYTE, (IntPtr)p);
            }
            _fontReady = true;
        }
        private static void DrawText(float x, float y, string text)
        {
            if (!_fontReady || string.IsNullOrEmpty(text)) return;
            int atlasW = FONT_COLS * FONT_CELL_W;
            int atlasH = FONT_ROWS * FONT_CELL_H;
            float cx = x;
            foreach (char ch in text)
            {
                if (ch < 32 || ch >= 128) { cx += FONT_CELL_W; continue; }
                int ci = ch - 32;
                int col = ci % FONT_COLS;
                int row = ci / FONT_COLS;
                float u0 = (float)(col * FONT_CELL_W) / atlasW;
                float v0 = (float)(row * FONT_CELL_H) / atlasH;
                float u1 = (float)((col + 1) * FONT_CELL_W) / atlasW;
                float v1 = (float)((row + 1) * FONT_CELL_H) / atlasH;
                float w = FONT_CELL_W;
                float h = FONT_CELL_H;
                _glBindTexture(GL_TEXTURE_2D, _fontTexId);
                _glBegin(GL_QUADS);
                _glTexCoord2f(u0, v0); _glVertex2f(cx, y - h);
                _glTexCoord2f(u1, v0); _glVertex2f(cx + w, y - h);
                _glTexCoord2f(u1, v1); _glVertex2f(cx + w, y);
                _glTexCoord2f(u0, v1); _glVertex2f(cx, y);
                _glEnd();
                cx += w;
            }
        }
        private static byte[] GetGlyph8x8(char c)
        {
            switch (c)
            {
                case ' ': return new byte[8];
                case '.': return new byte[] { 0, 0, 0, 0, 0, 0, 0x18, 0 };
                case ':': return new byte[] { 0, 0x18, 0x18, 0, 0, 0x18, 0x18, 0 };
                case '0': return new byte[] { 0x3C, 0x66, 0x6E, 0x76, 0x66, 0x66, 0x3C, 0 };
                case '1': return new byte[] { 0x18, 0x38, 0x18, 0x18, 0x18, 0x18, 0x3C, 0 };
                case '2': return new byte[] { 0x3C, 0x66, 0x06, 0x0C, 0x30, 0x60, 0x7E, 0 };
                case '3': return new byte[] { 0x3C, 0x66, 0x06, 0x1C, 0x06, 0x66, 0x3C, 0 };
                case '4': return new byte[] { 0x0C, 0x1C, 0x3C, 0x6C, 0x7E, 0x0C, 0x0C, 0 };
                case '5': return new byte[] { 0x7E, 0x60, 0x7C, 0x06, 0x06, 0x66, 0x3C, 0 };
                case '6': return new byte[] { 0x1C, 0x30, 0x60, 0x7C, 0x66, 0x66, 0x3C, 0 };
                case '7': return new byte[] { 0x7E, 0x66, 0x06, 0x0C, 0x18, 0x18, 0x18, 0 };
                case '8': return new byte[] { 0x3C, 0x66, 0x66, 0x3C, 0x66, 0x66, 0x3C, 0 };
                case '9': return new byte[] { 0x3C, 0x66, 0x66, 0x3E, 0x06, 0x0C, 0x38, 0 };
                case 'F': return new byte[] { 0x7E, 0x60, 0x60, 0x7C, 0x60, 0x60, 0x60, 0 };
                case 'P': return new byte[] { 0x7C, 0x66, 0x66, 0x7C, 0x60, 0x60, 0x60, 0 };
                case 'S': return new byte[] { 0x3C, 0x66, 0x60, 0x3C, 0x06, 0x66, 0x3C, 0 };
                case 'T': return new byte[] { 0x7E, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0 };
                case 'I': return new byte[] { 0x3C, 0x18, 0x18, 0x18, 0x18, 0x18, 0x3C, 0 };
                case 'M': return new byte[] { 0x66, 0x7E, 0x7E, 0x6E, 0x66, 0x66, 0x66, 0 };
                case 'A': return new byte[] { 0x3C, 0x66, 0x66, 0x7E, 0x66, 0x66, 0x66, 0 };
                case 'X': return new byte[] { 0x66, 0x66, 0x3C, 0x18, 0x3C, 0x66, 0x66, 0 };
                default: return new byte[] { 0x7E, 0x42, 0x42, 0x42, 0x42, 0x42, 0x7E, 0 };
            }
        }
        #endregion

        #region Core GL Load
        private static void EnsureCoreGL()
        {
            if (_coreLoaded) return;
            try
            {
                IntPtr hOGL = NativeMethods.GetModuleHandle("opengl32.dll");
                if (hOGL == IntPtr.Zero) return;
                T L<T>(string n) where T : class
                {
                    IntPtr p = NativeMethods.GetProcAddress(hOGL, n);
                    if (p == IntPtr.Zero) return null;
                    return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
                }
                _glGetIntegerv = L<glGetIntegervDelegate>("glGetIntegerv");
                _glReadPixels = L<glReadPixelsDelegate>("glReadPixels");

                _glGenBuffers = L<glGenBuffersDelegate>("glGenBuffers");
                _glBindBuffer = L<glBindBufferDelegate>("glBindBuffer");
                _glBufferData = L<glBufferDataDelegate>("glBufferData");
                _glMapBuffer = L<glMapBufferDelegate>("glMapBuffer");
                _glUnmapBuffer = L<glUnmapBufferDelegate>("glUnmapBuffer");
                _glDeleteBuffers = L<glDeleteBuffersDelegate>("glDeleteBuffers");

                _glGenFramebuffers = L<glGenFramebuffersDelegate>("glGenFramebuffers");
                _glBindFramebuffer = L<glBindFramebufferDelegate>("glBindFramebuffer");
                _glFramebufferTexture2D = L<glFramebufferTexture2DDelegate>("glFramebufferTexture2D");
                _glDeleteFramebuffers = L<glDeleteFramebuffersDelegate>("glDeleteFramebuffers");
                _glGenTextures = L<glGenTexturesDelegate>("glGenTextures");
                _glBindTexture = L<glBindTextureDelegate>("glBindTexture");
                _glTexParameteri = L<glTexParameteriDelegate>("glTexParameteri");
                _glTexImage2D = L<glTexImage2DDelegate>("glTexImage2D");
                _glBlitFramebuffer = L<glBlitFramebufferDelegate>("glBlitFramebuffer");

                _coreLoaded = _glGetIntegerv != null && _glReadPixels != null;
                Logging.Info("[CoreGL] Loaded=" + _coreLoaded);
            }
            catch (Exception ex) { Logging.Exception("EnsureCoreGL", ex); }
        }
        #endregion

        #region Helpers
        private static IntPtr EnsureModule(string name)
        {
            IntPtr h = NativeMethods.GetModuleHandle(name);
            if (h == IntPtr.Zero) h = NativeMethods.LoadLibrary(name);
            return h;
        }
        private static void HookExport(IntPtr module, string export, Action<IntPtr> cb)
        {
            try
            {
                IntPtr addr = NativeMethods.GetProcAddress(module, export);
                if (addr == IntPtr.Zero)
                {
                    Logging.Warn("[Export] " + export + " 未找到");
                    return;
                }
                cb(addr);
            }
            catch (Exception ex) { Logging.Exception("HookExport " + export, ex); }
        }
        private static string FormatRuntime(double sec)
        {
            if (sec < 3600)
            {
                int m = (int)(sec / 60);
                int s = (int)(sec % 60);
                return $"{m:D2}:{s:D2}";
            }
            int h = (int)(sec / 3600);
            int rem = (int)(sec % 3600);
            int mm = rem / 60;
            int ss = rem % 60;
            return $"{h:D2}:{mm:D2}:{ss:D2}";
        }
        #endregion

        #region Frame Ring
        private class AsyncFrame
        {
            public byte[] Pixels;
            public int Width;
            public int Height;
            public int FrameIndex;
            public long TimestampTicks;
            public void EnsureSize(int w, int h)
            {
                int need = w * h * 4;
                if (Pixels == null || Pixels.Length != need)
                    Pixels = new byte[need];
            }
        }
        private class FrameRing
        {
            private readonly AsyncFrame[] _frames;
            private volatile int _lastPublished = -1;
            public FrameRing(int cap)
            {
                _frames = new AsyncFrame[cap];
                for (int i = 0; i < cap; i++) _frames[i] = new AsyncFrame();
            }
            public AsyncFrame GetSlot(int fi) => _frames[fi % _frames.Length];
            public void Publish(int fi) => Volatile.Write(ref _lastPublished, fi);
            public AsyncFrame TryGetLatest(int last)
            {
                int pub = Volatile.Read(ref _lastPublished);
                if (pub <= last) return null;
                return _frames[pub % _frames.Length];
            }
        }
        #endregion
    }
}