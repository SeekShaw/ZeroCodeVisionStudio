using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.MinMaxGray
{
    /// <summary>
    /// 灰度最值算子参数。
    /// </summary>
    public class MinMaxGrayParameter : BindableBase, IHandleParameter
    {
        private double percent = 0.0;

        [DisplayName("过滤百分比 (%)")]
        [Description("排除直方图两端极值噪点的百分比 (0~50)。0 表示严格绝对最值，大于 0 可提升抗噪鲁棒性。")]
        public double Percent { get => percent; set => SetProperty(ref percent, value); }

        public IHandleParameter Clone() => new MinMaxGrayParameter
        {
            Percent = Percent
        };

        public void Reset()
        {
            Percent = 0.0;
        }
    }
}
