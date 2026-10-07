using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Threshold
{
    public class ThresholdParameter : BindableBase, IHandleParameter
    {
        private double minGray = 0;
        private double maxGray = 128;

        [DisplayName("最小灰度")]
        [Description("阈值分割下限")]
        [Range((double)0, (double)255)]
        public double MinGray { get => minGray; set => SetProperty(ref minGray, value); }

        [DisplayName("最大灰度")]
        [Description("阈值分割上限")]
        [Range((double)0, (double)255)]
        public double MaxGray { get => maxGray; set => SetProperty(ref maxGray, value); }

        public IHandleParameter Clone() => new ThresholdParameter { MinGray = MinGray, MaxGray = MaxGray };

        public void Reset()
        {
            MinGray = 0;
            MaxGray = 128;
        }
    }
}
