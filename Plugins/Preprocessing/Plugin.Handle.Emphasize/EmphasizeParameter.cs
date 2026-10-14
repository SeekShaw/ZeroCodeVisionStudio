using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Emphasize
{
    /// <summary>
    /// 图像增强算子的参数类。
    /// 定义用于计算局部均值的高通掩膜尺寸和增强因子。
    /// </summary>
    public class EmphasizeParameter : BindableBase, IHandleParameter
    {
        private int maskWidth = 7;
        private int maskHeight = 7;
        private double factor = 1.0;

        [DisplayName("掩码宽度")]
        [Description("高通滤波掩膜宽度")]
        public int MaskWidth { get => maskWidth; set => SetProperty(ref maskWidth, value); }

        [DisplayName("掩码高度")]
        [Description("高通滤波掩膜高度")]
        public int MaskHeight { get => maskHeight; set => SetProperty(ref maskHeight, value); }

        [DisplayName("增强因子")]
        [Description("对比度增强因子，通常 0~10")]
        public double Factor { get => factor; set => SetProperty(ref factor, value); }

        public IHandleParameter Clone() => new EmphasizeParameter { MaskWidth = MaskWidth, MaskHeight = MaskHeight, Factor = Factor };

        public void Reset()
        {
            MaskWidth = 7;
            MaskHeight = 7;
            Factor = 1.0;
        }
    }
}
