using System;

namespace HookLib
{
    internal class AsyncFrame
    {
        public byte[] Pixels; // BGRA
        public int Width;
        public int Height;
        public int FrameIndex;
        public long TimestampTicks;

        public void EnsureSize(int w, int h)
        {
            int need = w * h * 4;
            if (Pixels == null || Pixels.Length != need)
                Pixels = new byte[need];
            Width = w;
            Height = h;
        }
    }
}