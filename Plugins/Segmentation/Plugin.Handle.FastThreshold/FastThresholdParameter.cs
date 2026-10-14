using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FastThreshold
{
    public class FastThresholdParameter : BindableBase, IHandleParameter
    {
        private double minGray = 0;
        private double maxGray = 80;
        private int minSize = 20;

        [DisplayName("最小灰度")]
        [Description("快速阈值下限")]
        public double MinGray { get => minGray; set => SetProperty(ref minGray, value); }

        [DisplayName("最大灰度")]
        [Description("快速阈值上限")]
        public double MaxGray { get => maxGray; set => SetProperty(ref maxGray, value); }

        [DisplayName("最小尺寸")]
        [Description("忽略小于该尺寸的区域")]
        public int MinSize { get => minSize; set => SetProperty(ref minSize, value); }

        public IHandleParameter Clone() => new FastThresholdParameter { MinGray = MinGray, MaxGray = MaxGray, MinSize = MinSize };

        public void Reset()
        {
            MinGray = 0;
            MaxGray = 80;
            MinSize = 20;
        }
    }
}
