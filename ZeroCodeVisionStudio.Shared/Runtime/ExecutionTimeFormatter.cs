using System;

namespace ZeroCodeVisionStudio.Shared
{
    public static class ExecutionTimeFormatter
    {
        public static string Format(TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
            {
                return "0 ms";
            }

            if (elapsed.TotalMilliseconds < 1000)
            {
                return $"{elapsed.TotalMilliseconds:0.##} ms";
            }

            if (elapsed.TotalMinutes < 1)
            {
                return $"{elapsed.TotalSeconds:0.###} s";
            }

            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";
        }
    }
}
