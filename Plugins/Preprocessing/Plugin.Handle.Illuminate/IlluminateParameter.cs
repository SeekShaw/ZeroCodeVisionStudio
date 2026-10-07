using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Illuminate
{
    public class IlluminateParameter : BindableBase, IHandleParameter
    {
        private int maskWidth = 101;
        private int maskHeight = 101;
        private double factor = 0.7;

        [DisplayName("掩码宽度")]
        [Description("光照校正局部均值掩膜宽度")]
        public int MaskWidth { get => maskWidth; set => SetProperty(ref maskWidth, value); }

        [DisplayName("掩码高度")]
        [Description("光照校正局部均值掩膜高度")]
        public int MaskHeight { get => maskHeight; set => SetProperty(ref maskHeight, value); }

        [DisplayName("增强因子")]
        [Description("光照校正增强因子")]
        public double Factor { get => factor; set => SetProperty(ref factor, value); }

        public IHandleParameter Clone() => new IlluminateParameter { MaskWidth = MaskWidth, MaskHeight = MaskHeight, Factor = Factor };

        public void Reset()
        {
            MaskWidth = 101;
            MaskHeight = 101;
            Factor = 0.7;
        }
    }
}
