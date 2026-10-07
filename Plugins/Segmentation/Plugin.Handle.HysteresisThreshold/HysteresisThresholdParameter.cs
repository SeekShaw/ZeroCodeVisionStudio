using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.HysteresisThreshold
{
    public class HysteresisThresholdParameter : BindableBase, IHandleParameter
    {
        private double low = 20;
        private double high = 40;
        private int maxLength = 10;

        [DisplayName("低阈值")]
        [Description("滞后阈值下限")]
        public double Low { get => low; set => SetProperty(ref low, value); }

        [DisplayName("高阈值")]
        [Description("滞后阈值上限")]
        public double High { get => high; set => SetProperty(ref high, value); }

        [DisplayName("最大长度")]
        [Description("连接弱边缘时允许的最大长度")]
        public int MaxLength { get => maxLength; set => SetProperty(ref maxLength, value); }

        public IHandleParameter Clone() => new HysteresisThresholdParameter { Low = Low, High = High, MaxLength = MaxLength };

        public void Reset()
        {
            Low = 20;
            High = 40;
            MaxLength = 10;
        }
    }
}
