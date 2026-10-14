using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.VarThreshold
{
    public class VarThresholdParameter : BindableBase, IHandleParameter
    {
        private int maskWidth = 15;
        private int maskHeight = 15;
        private double stdDevScale = 0.2;
        private double absThreshold = 10;
        private DynThresholdMode lightDark = DynThresholdMode.dark;

        [DisplayName("掩码宽度")]
        [Description("局部方差统计窗口宽度")]
        public int MaskWidth { get => maskWidth; set => SetProperty(ref maskWidth, value); }

        [DisplayName("掩码高度")]
        [Description("局部方差统计窗口高度")]
        public int MaskHeight { get => maskHeight; set => SetProperty(ref maskHeight, value); }

        [DisplayName("标准差比例")]
        [Description("相对局部标准差的比例阈值")]
        public double StdDevScale { get => stdDevScale; set => SetProperty(ref stdDevScale, value); }

        [DisplayName("绝对阈值")]
        [Description("绝对灰度阈值下限")]
        public double AbsThreshold { get => absThreshold; set => SetProperty(ref absThreshold, value); }

        [DisplayName("亮暗模式")]
        [Description("相对局部背景提取的目标极性")]
        public DynThresholdMode LightDark { get => lightDark; set => SetProperty(ref lightDark, value); }

        public IHandleParameter Clone()
        {
            return new VarThresholdParameter
            {
                MaskWidth = MaskWidth,
                MaskHeight = MaskHeight,
                StdDevScale = StdDevScale,
                AbsThreshold = AbsThreshold,
                LightDark = LightDark
            };
        }

        public void Reset()
        {
            MaskWidth = 15;
            MaskHeight = 15;
            StdDevScale = 0.2;
            AbsThreshold = 10;
            LightDark = DynThresholdMode.dark;
        }
    }
}
