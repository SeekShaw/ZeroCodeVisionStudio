using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.EdgesSubPix
{
    public class EdgesSubPixParameter : BindableBase, IHandleParameter
    {
        private EdgesFilterType filter = EdgesFilterType.Canny;
        private double alpha = 1.0;
        private double low = 20;
        private double high = 40;

        [DisplayName("滤波器")]
        [Description("亚像素边缘提取使用的滤波器类型")]
        public EdgesFilterType Filter { get => filter; set => SetProperty(ref filter, value); }

        [DisplayName("平滑系数")]
        [Description("滤波器平滑系数 Alpha，值越大边缘越平滑")]
        public double Alpha { get => alpha; set => SetProperty(ref alpha, value); }

        [DisplayName("低阈值")]
        [Description("滞后阈值的下限")]
        public double Low { get => low; set => SetProperty(ref low, value); }

        [DisplayName("高阈值")]
        [Description("滞后阈值的上限")]
        public double High { get => high; set => SetProperty(ref high, value); }

        public IHandleParameter Clone() => new EdgesSubPixParameter { Filter = Filter, Alpha = Alpha, Low = Low, High = High };

        public void Reset()
        {
            Filter = EdgesFilterType.Canny;
            Alpha = 1.0;
            Low = 20;
            High = 40;
        }
    }
}
