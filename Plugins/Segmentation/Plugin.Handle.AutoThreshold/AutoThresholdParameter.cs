using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.AutoThreshold
{
    public class AutoThresholdParameter : BindableBase, IHandleParameter
    {
        private double sigma = 2.0;

        [DisplayName("Sigma")]
        [Description("直方图平滑高斯核尺度")]
        public double Sigma { get => sigma; set => SetProperty(ref sigma, value); }

        public IHandleParameter Clone() => new AutoThresholdParameter { Sigma = Sigma };

        public void Reset()
        {
            Sigma = 2.0;
        }
    }
}
