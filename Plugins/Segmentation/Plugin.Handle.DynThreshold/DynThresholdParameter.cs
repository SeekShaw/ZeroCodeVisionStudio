using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.DynThreshold
{
    public class DynThresholdParameter : BindableBase, IHandleParameter
    {
        private int maskWidth = 25;
        private int maskHeight = 25;
        private double offset = 5;
        private DynThresholdMode lightDark = DynThresholdMode.dark;

        [DisplayName("掩码宽度")]
        [Description("动态阈值局部均值掩膜宽度")]
        public int MaskWidth { get => maskWidth; set => SetProperty(ref maskWidth, value); }

        [DisplayName("掩码高度")]
        [Description("动态阈值局部均值掩膜高度")]
        public int MaskHeight { get => maskHeight; set => SetProperty(ref maskHeight, value); }

        [DisplayName("偏移量")]
        [Description("相对局部背景的灰度偏移")]
        public double Offset { get => offset; set => SetProperty(ref offset, value); }

        [DisplayName("亮暗模式")]
        [Description("相对局部背景提取的目标极性")]
        public DynThresholdMode LightDark { get => lightDark; set => SetProperty(ref lightDark, value); }

        public IHandleParameter Clone()
        {
            return new DynThresholdParameter
            {
                MaskWidth = MaskWidth,
                MaskHeight = MaskHeight,
                Offset = Offset,
                LightDark = LightDark
            };
        }

        public void Reset()
        {
            MaskWidth = 25;
            MaskHeight = 25;
            Offset = 5;
            LightDark = DynThresholdMode.dark;
        }
    }
}
