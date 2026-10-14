using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ShapeTrans
{
    public class ShapeTransParameter : BindableBase, IHandleParameter
    {
        private ShapeTransMode mode = ShapeTransMode.Convex;

        [DisplayName("变换模式")]
        [Description("区域形状变换模式：凸包、轴对齐外接矩形、任意角外接矩形、内接圆、外接圆")]
        public ShapeTransMode Mode { get => mode; set => SetProperty(ref mode, value); }

        public IHandleParameter Clone() => new ShapeTransParameter { Mode = Mode };

        public void Reset()
        {
            Mode = ShapeTransMode.Convex;
        }
    }
}
