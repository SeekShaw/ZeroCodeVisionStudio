using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FindShapeModel
{
    public class FindShapeModelParameter : BindableBase, IHandleParameter
    {
        private double angleStartDeg = -30;
        private double angleExtentDeg = 60;
        private double minScore = 0.5;
        private int numMatches = 1;
        private double maxOverlap = 0.5;
        private ShapeModelSubPixel subPixel = ShapeModelSubPixel.LeastSquares;
        private int numLevels = 0;
        private double greediness = 0.9;

        [DisplayName("起始角度(°)")]
        [Description("搜索时允许的起始旋转角度（度）。默认 -30°。")]
        public double AngleStartDeg { get => angleStartDeg; set => SetProperty(ref angleStartDeg, value); }

        [DisplayName("角度范围(°)")]
        [Description("从起始角度起允许的搜索旋转范围（度）。默认 60°。")]
        public double AngleExtentDeg { get => angleExtentDeg; set => SetProperty(ref angleExtentDeg, value); }

        [DisplayName("最小分数")]
        [Description("匹配分数阈值，范围通常 0~1。分数越高结果越严格，默认 0.5。")]
        [Range((double)0, (double)1)]
        public double MinScore { get => minScore; set => SetProperty(ref minScore, value); }

        [DisplayName("匹配数量")]
        [Description("最多返回的匹配实例数。0 表示不限制数量。")]
        [Range(1, 1000)]
        public int NumMatches { get => numMatches; set => SetProperty(ref numMatches, value); }

        [DisplayName("最大重叠")]
        [Description("允许的匹配结果最大重叠比例，范围 0~1。默认 0.5。")]
        public double MaxOverlap { get => maxOverlap; set => SetProperty(ref maxOverlap, value); }

        [DisplayName("亚像素模式")]
        [Description("匹配结果的亚像素精化方式：最小二乘、插值或不做亚像素。")]
        public ShapeModelSubPixel SubPixel { get => subPixel; set => SetProperty(ref subPixel, value); }

        [DisplayName("金字塔层数")]
        [Description("搜索使用的金字塔层数。0 表示使用创建模板时的全部层数。")]
        public int NumLevels { get => numLevels; set => SetProperty(ref numLevels, value); }

        [DisplayName("贪婪度")]
        [Description("搜索贪婪度，范围 0~1。越大速度越快但可能漏检；默认 0.9。")]
        [Range((double)0, (double)1)]
        public double Greediness { get => greediness; set => SetProperty(ref greediness, value); }

        public IHandleParameter Clone()
        {
            return new FindShapeModelParameter
            {
                AngleStartDeg = AngleStartDeg,
                AngleExtentDeg = AngleExtentDeg,
                MinScore = MinScore,
                NumMatches = NumMatches,
                MaxOverlap = MaxOverlap,
                SubPixel = SubPixel,
                NumLevels = NumLevels,
                Greediness = Greediness
            };
        }

        public void Reset()
        {
            AngleStartDeg = -30;
            AngleExtentDeg = 60;
            MinScore = 0.5;
            NumMatches = 1;
            MaxOverlap = 0.5;
            SubPixel = ShapeModelSubPixel.LeastSquares;
            NumLevels = 0;
            Greediness = 0.9;
        }
    }
}
