using System;
using System.Threading;

namespace HookLib
{
    internal class FrameRing
    {
        private readonly AsyncFrame[] _frames;
        private int _write;
        private int _lastPublished = -1;

        public FrameRing(int capacity)
        {
            _frames = new AsyncFrame[capacity];
            for (int i = 0; i < capacity; i++)
                _frames[i] = new AsyncFrame();
        }

        // Producer：返回一个可写帧
        public AsyncFrame AcquireForWrite()
        {
            int idx = Interlocked.Increment(ref _write);
            return _frames[idx % _frames.Length];
        }

        // Producer：发布这帧（消费者可以看到）
        public void Publish(AsyncFrame f)
        {
            Volatile.Write(ref _lastPublished, f.FrameIndex);
        }

        // Consumer：拿最新帧（如果没有新帧或已经处理过，返回 null）
        public AsyncFrame TryGetLatest(int lastProcessedFrame)
        {
            int pub = Volatile.Read(ref _lastPublished);
            if (pub <= lastProcessedFrame) return null;
            // 寻址
            return _frames[pub % _frames.Length];
        }
    }
}