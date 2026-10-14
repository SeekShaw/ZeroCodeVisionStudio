using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.LocalThreshold
{
    public class LocalThresholdParameter : BindableBase, IHandleParameter
    {
        private int maskWidth = 15;
        private int maskHeight = 15;
        private double scale = 0.2;
        private ELightDark lightDark = ELightDark.dark;

        [DisplayName("掩码宽度")]
        [Description("局部统计窗口宽度（像素）")]
        public int MaskWidth { get => maskWidth; set => SetProperty(ref maskWidth, value); }

        [DisplayName("掩码高度")]
        [Description("局部统计窗口高度（像素）")]
        public int MaskHeight { get => maskHeight; set => SetProperty(ref maskHeight, value); }

        [DisplayName("比例")]
        [Description("相对局部均值的比例阈值")]
        public double Scale { get => scale; set => SetProperty(ref scale, value); }

        [DisplayName("亮暗极性")]
        [Description("提取暗目标或亮目标")]
        public ELightDark LightDark { get => lightDark; set => SetProperty(ref lightDark, value); }

        public IHandleParameter Clone() => new LocalThresholdParameter
        {
            MaskWidth = MaskWidth,
            MaskHeight = MaskHeight,
            Scale = Scale,
            LightDark = LightDark
        };

        public void Reset()
        {
            MaskWidth = 15;
            MaskHeight = 15;
            Scale = 0.2;
            LightDark = ELightDark.dark;
        }
    }
}
