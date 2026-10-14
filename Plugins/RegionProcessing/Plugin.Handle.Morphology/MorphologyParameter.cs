using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Morphology
{
    public class MorphologyParameter : BindableBase, IHandleParameter
    {
        private MorphologyOperation operation = MorphologyOperation.ClosingCircle;
        private double radius = 3.5;

        [DisplayName("运算类型")]
        [Description("形态学运算类型：开运算/闭运算/膨胀/腐蚀（圆形结构元素）")]
        public MorphologyOperation Operation { get => operation; set => SetProperty(ref operation, value); }

        [DisplayName("半径")]
        [Description("圆形结构元素的半径（像素）")]
        public double Radius { get => radius; set => SetProperty(ref radius, value); }

        public IHandleParameter Clone()
        {
            return new MorphologyParameter
            {
                Operation = Operation,
                Radius = Radius
            };
        }

        public void Reset()
        {
            Operation = MorphologyOperation.ClosingCircle;
            Radius = 3.5;
        }
    }
}
