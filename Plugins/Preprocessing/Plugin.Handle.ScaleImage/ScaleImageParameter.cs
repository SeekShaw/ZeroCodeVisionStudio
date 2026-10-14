using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.ScaleImage
{
    public class ScaleImageParameter : BindableBase, IHandleParameter
    {
        private double multiplier = 1.0;
        private double add;

        [DisplayName("乘数")]
        [Description("灰度线性变换乘数 Mult")]
        public double Multiplier { get => multiplier; set => SetProperty(ref multiplier, value); }

        [DisplayName("加数")]
        [Description("灰度线性变换加数 Add")]
        public double Add { get => add; set => SetProperty(ref add, value); }

        public IHandleParameter Clone() => new ScaleImageParameter { Multiplier = Multiplier, Add = Add };

        public void Reset()
        {
            Multiplier = 1.0;
            Add = 0;
        }
    }
}
