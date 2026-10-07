using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.MedianImage
{
    /// <summary>
    /// 中值滤波算子的参数类。
    /// 定义掩膜类型和掩膜半径。
    /// </summary>
    public class MedianImageParameter : BindableBase, IHandleParameter
    {
        private MedianMaskType maskType = MedianMaskType.Circle;
        private int radius = 3;

        [DisplayName("掩膜类型")]
        [Description("中值滤波掩膜形状")]
        public MedianMaskType MaskType { get => maskType; set => SetProperty(ref maskType, value); }

        [DisplayName("半径")]
        [Description("中值滤波掩膜半径（像素）")]
        public int Radius { get => radius; set => SetProperty(ref radius, value); }

        public IHandleParameter Clone() => new MedianImageParameter { MaskType = MaskType, Radius = Radius };

        public void Reset()
        {
            MaskType = MedianMaskType.Circle;
            Radius = 3;
        }
    }
}
