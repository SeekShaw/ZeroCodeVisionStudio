using System.Collections.Generic;
using HalconDotNet;

namespace ZeroCodeVisionStudio.Shared
{
    /// <summary>
    /// HALCON HTuple 辅助工具类。
    /// 提供常用的 HALCON 数值转换静态方法，封装角度/弧度和"auto"元组的创建逻辑，
    /// 减少各算子中重复的转换代码。
    /// </summary>
    public static class HalconTupleHelper
    {
        /// <summary>
        /// 将角度值（度）转换为弧度 HTuple。
        /// 等价于 HALCON 的 TupleRad 算子。
        /// </summary>
        /// <param name="degrees">角度值（度，Double 类型）。</param>
        /// <returns>对应的弧度 HTuple。</returns>
        public static HTuple ToRadians(double degrees)
        {
            using var degreeTuple = new HTuple(degrees);
            return degreeTuple.TupleRad();
        }

        /// <summary>
        /// 将角度步长（度）转换为弧度 HTuple；若值 &lt;= 0，则返回 "auto" 字符串元组。
        /// 用于形状模型创建等需要"自动角度步长"的场景：传入 0 或负数时，
        /// HALCON 会根据模型复杂度自动计算最优步长。
        /// </summary>
        /// <param name="degrees">角度步长（度）；&lt;= 0 时返回 "auto"。</param>
        /// <returns>"auto" HTuple 或对应的弧度 HTuple。</returns>
        public static HTuple ToAngleStep(double degrees)
        {
            if (degrees <= 0)
            {
                return new HTuple("auto");
            }

            using var degreeTuple = new HTuple(degrees);
            return degreeTuple.TupleRad();
        }

        /// <summary>
        /// 将字符串转换为 HTuple：
        /// - 空/null → "auto"；
        /// - "auto"（忽略大小写）→ "auto"；
        /// - 纯数字（如 "30"）→ 数值元组；
        /// - 逗号分隔数字列表（如 "30,60,15"，即 HALCON 的 low,high,minSize 形式）→ 多元素数值元组；
        /// - 其它（如 pregeneration 等枚举字符串）→ 原样字符串元组。
        /// <para>
        /// 注意：HALCON 的 Contrast 等控制参数只接受 "auto" 字符串或数值元组，
        /// 把 "30" 或 "30,60,15" 当字符串传入会报 #1307（Wrong value of control parameter）。
        /// </para>
        /// </summary>
        public static HTuple ToAutoTuple(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new HTuple("auto");
            }

            var trimmed = value.Trim();
            if (string.Equals(trimmed, "auto", System.StringComparison.OrdinalIgnoreCase))
            {
                return new HTuple("auto");
            }

            // 逗号分隔的数值列表 → 数值元组（HALCON low,high,minSize 形式）
            if (trimmed.Contains(','))
            {
                var numbers = new List<double>();
                foreach (var part in trimmed.Split(','))
                {
                    if (double.TryParse(part.Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var number))
                    {
                        numbers.Add(number);
                    }
                }

                if (numbers.Count > 0)
                {
                    return new HTuple(numbers.ToArray());
                }
            }
            else if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out var single))
            {
                return new HTuple(single);
            }

            return new HTuple(trimmed);
        }
    }
}
