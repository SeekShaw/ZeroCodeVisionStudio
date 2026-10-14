using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.OpeningRectangle
{
    public class OpeningRectangleParameter : BindableBase, IHandleParameter
    {
        private int width = 5;
        private int height = 5;

        [DisplayName("宽度")]
        [Description("矩形结构元素的宽度（像素）")]
        public int Width { get => width; set => SetProperty(ref width, value); }

        [DisplayName("高度")]
        [Description("矩形结构元素的高度（像素）")]
        public int Height { get => height; set => SetProperty(ref height, value); }

        public IHandleParameter Clone() => new OpeningRectangleParameter { Width = Width, Height = Height };

        public void Reset()
        {
            Width = 5;
            Height = 5;
        }
    }
}
