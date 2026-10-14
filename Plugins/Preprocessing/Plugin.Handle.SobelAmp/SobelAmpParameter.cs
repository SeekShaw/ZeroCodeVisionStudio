using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.SobelAmp
{
    /// <summary>
    /// Sobel 梯度幅值算子的参数类。
    /// 定义滤波器类型和掩膜尺寸。
    /// </summary>
    public class SobelAmpParameter : BindableBase, IHandleParameter
    {
        private SobelFilterType filterType = SobelFilterType.SumAbs;
        private int size = 3;

        [DisplayName("滤波器类型")]
        [Description("Sobel 幅值计算方式")]
        public SobelFilterType FilterType { get => filterType; set => SetProperty(ref filterType, value); }

        [DisplayName("尺寸")]
        [Description("Sobel 掩膜尺寸，须为奇数，最小 3")]
        public int Size { get => size; set => SetProperty(ref size, value); }

        public IHandleParameter Clone() => new SobelAmpParameter { FilterType = FilterType, Size = Size };

        public void Reset()
        {
            FilterType = SobelFilterType.SumAbs;
            Size = 3;
        }
    }
}
