namespace NodeGraphControl {
    public class Utils {
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
    }
}