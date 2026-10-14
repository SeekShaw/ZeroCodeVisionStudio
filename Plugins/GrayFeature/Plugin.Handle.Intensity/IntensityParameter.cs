using System;
using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Intensity
{
    /// <summary>
    /// 平均灰度算子参数。
    /// </summary>
    public class IntensityParameter : BindableBase, IHandleParameter
    {
        private double expectedMean = 128.0;
        private double tolerance = 255.0;

        [DisplayName("期望灰度均值")]
        [Description("检测目标的期望平均灰度参考值 (0~255)。")]
        public double ExpectedMean
        {
            get => expectedMean;
            set => SetProperty(ref expectedMean, Math.Clamp(value, 0.0, 255.0));
        }

        [DisplayName("允许灰度容差")]
        [Description("允许偏离期望均值的最大偏差范围 (0~255)。")]
        public double Tolerance
        {
            get => tolerance;
            set => SetProperty(ref tolerance, Math.Clamp(value, 0.0, 255.0));
        }

        public IHandleParameter Clone() => new IntensityParameter
        {
            ExpectedMean = ExpectedMean,
            Tolerance = Tolerance
        };

        public void Reset()
        {
            ExpectedMean = 128.0;
            Tolerance = 255.0;
        }
    }
}
