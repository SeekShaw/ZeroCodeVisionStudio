using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.TopHat
{
    public class TopHatParameter : BindableBase, IHandleParameter
    {
        private double radius = 9;

        [DisplayName("半径")]
        [Description("圆形结构元素半径")]
        public double Radius { get => radius; set => SetProperty(ref radius, value); }

        public IHandleParameter Clone() => new TopHatParameter { Radius = Radius };

        public void Reset()
        {
            Radius = 9;
        }
    }
}
