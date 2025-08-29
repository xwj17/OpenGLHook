using System;

namespace HookLib
{
    internal struct Box
    {
        public float X1, Y1, X2, Y2;
        public float Score;

        public float Area => Math.Max(0, X2 - X1) * Math.Max(0, Y2 - Y1);

        public float IoU(in Box other)
        {
            float xx1 = Math.Max(X1, other.X1);
            float yy1 = Math.Max(Y1, other.Y1);
            float xx2 = Math.Min(X2, other.X2);
            float yy2 = Math.Min(Y2, other.Y2);
            float w = Math.Max(0, xx2 - xx1);
            float h = Math.Max(0, yy2 - yy1);
            float inter = w * h;
            float union = Area + other.Area - inter + 1e-6f;
            return inter / union;
        }
    }
}