using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.GrayOpeningRect
{
    /// <summary>
    /// 灰度矩形开运算算子的参数类。
    /// 定义矩形结构元素的宽度和高度。
    /// </summary>
    public class GrayOpeningRectParameter : BindableBase, IHandleParameter
    {
        private int width = 5;
        private int height = 5;

        [DisplayName("宽度")]
        [Description("矩形结构元素宽度")]
        public int Width { get => width; set => SetProperty(ref width, value); }

        [DisplayName("高度")]
        [Description("矩形结构元素高度")]
        public int Height { get => height; set => SetProperty(ref height, value); }

        public IHandleParameter Clone() => new GrayOpeningRectParameter { Width = Width, Height = Height };

        public void Reset()
        {
            Width = 5;
            Height = 5;
        }
    }
}
