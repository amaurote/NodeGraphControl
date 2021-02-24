using System;
using System.Drawing;

namespace NodeGraphControl {
    public static class Utils {
        public static float Clamp(float min, float max, float value) {
            if (value < min)
                return min;
            if (value > max)
                return max;

            return value;
        }

        public static int Clamp(int min, int max, int value) {
            if (value < min)
                return min;
            if (value > max)
                return max;

            return value;
        }

        public static double Distance(PointF p1, PointF p2) {
            double xDelta = p1.X - p2.X;
            double yDelta = p1.Y - p2.Y;

            return Math.Sqrt(Math.Pow(xDelta, 2) + Math.Pow(yDelta, 2));
        }
    }
}