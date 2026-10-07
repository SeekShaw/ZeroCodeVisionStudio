using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.MeanImage
{
    /// <summary>
    /// 均值滤波算子的参数类。
    /// 定义均值滤波掩膜的宽度和高度。
    /// </summary>
    public class MeanImageParameter : BindableBase, IHandleParameter
    {
        private int maskWidth = 5;
        private int maskHeight = 5;

        [DisplayName("掩码宽度")]
        [Description("均值滤波掩膜宽度，建议为正奇数")]
        [Range(1, 511)]
        public int MaskWidth { get => maskWidth; set => SetProperty(ref maskWidth, value); }

        [DisplayName("掩码高度")]
        [Description("均值滤波掩膜高度，建议为正奇数")]
        [Range(1, 511)]
        public int MaskHeight { get => maskHeight; set => SetProperty(ref maskHeight, value); }

        public IHandleParameter Clone() => new MeanImageParameter { MaskWidth = MaskWidth, MaskHeight = MaskHeight };

        public void Reset()
        {
            MaskWidth = 5;
            MaskHeight = 5;
        }
    }
}
