using System.ComponentModel;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.DefectMeasure
{
    public class DefectMeasureParameter : BindableBase, IHandleParameter
    {
        private int allowedMaxCount;
        private double allowedMaxTotalArea;
        private double allowedMaxSingleArea;
        private double minAreaFilter = 5.0;

        [DisplayName("允许最大缺陷数")]
        [Description("缺陷连通域数量上限，0 表示不允许任何缺陷")]
        public int AllowedMaxCount { get => allowedMaxCount; set => SetProperty(ref allowedMaxCount, value); }

        [DisplayName("允许最大总面积")]
        [Description("缺陷总面积上限（像素），0 表示不限制")]
        public double AllowedMaxTotalArea { get => allowedMaxTotalArea; set => SetProperty(ref allowedMaxTotalArea, value); }

        [DisplayName("单缺陷最大面积")]
        [Description("任一缺陷连通域面积超过该值即 NG（像素），0 表示不限制。大缺陷往往比多个小缺陷更致命，可单独卡上限")]
        public double AllowedMaxSingleArea { get => allowedMaxSingleArea; set => SetProperty(ref allowedMaxSingleArea, value); }

        [DisplayName("最小噪点面积过滤")]
        [Description("忽略小于该面积的微小噪点缺陷（像素）")]
        public double MinAreaFilter { get => minAreaFilter; set => SetProperty(ref minAreaFilter, value); }

        public IHandleParameter Clone()
        {
            return new DefectMeasureParameter
            {
                AllowedMaxCount = AllowedMaxCount,
                AllowedMaxTotalArea = AllowedMaxTotalArea,
                AllowedMaxSingleArea = AllowedMaxSingleArea,
                MinAreaFilter = MinAreaFilter
            };
        }

        public void Reset()
        {
            AllowedMaxCount = 0;
            AllowedMaxTotalArea = 0;
            AllowedMaxSingleArea = 0;
            MinAreaFilter = 5.0;
        }
    }
}
