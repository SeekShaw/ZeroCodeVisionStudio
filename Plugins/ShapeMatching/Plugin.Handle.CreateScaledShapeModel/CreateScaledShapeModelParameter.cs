using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.CreateScaledShapeModel
{
    public class CreateScaledShapeModelParameter : BindableBase, IHandleParameter
    {
        private int numLevels = 4;
        private double angleStartDeg = -30;
        private double angleExtentDeg = 60;
        private double angleStepDeg;
        private double scaleMin = 0.8;
        private double scaleMax = 1.2;
        private double scaleStep;
        private string optimization = "auto";
        private ShapeModelMetric metric = ShapeModelMetric.UsePolarity;
        private string contrast = "auto";
        private string minContrast = "auto";

        [DisplayName("金字塔层数")]
        [Description("形状模板的金字塔层数。数值越大搜索越快但精度可能下降；建议 3~5，常用 4。")]
        public int NumLevels { get => numLevels; set => SetProperty(ref numLevels, value); }

        [DisplayName("起始角度(°)")]
        [Description("模板可旋转搜索的起始角度（度）。默认 -30°。")]
        public double AngleStartDeg { get => angleStartDeg; set => SetProperty(ref angleStartDeg, value); }

        [DisplayName("角度范围(°)")]
        [Description("从起始角度起允许的旋转范围（度）。默认 60°，即覆盖 -30°~30°。")]
        public double AngleExtentDeg { get => angleExtentDeg; set => SetProperty(ref angleExtentDeg, value); }

        [DisplayName("角度步长(°)")]
        [Description("角度采样步长（度）。<=0 时使用 HALCON 自动步长（auto）。")]
        public double AngleStepDeg { get => angleStepDeg; set => SetProperty(ref angleStepDeg, value); }

        [DisplayName("最小缩放")]
        [Description("模板允许的最小等比缩放系数。默认 0.8。")]
        public double ScaleMin { get => scaleMin; set => SetProperty(ref scaleMin, value); }

        [DisplayName("最大缩放")]
        [Description("模板允许的最大等比缩放系数。默认 1.2。")]
        public double ScaleMax { get => scaleMax; set => SetProperty(ref scaleMax, value); }

        [DisplayName("缩放步长")]
        [Description("缩放采样步长。<=0 时使用 HALCON 自动步长（auto）。")]
        public double ScaleStep { get => scaleStep; set => SetProperty(ref scaleStep, value); }

        [DisplayName("优化方式")]
        [Description("模板点集优化策略。可填 auto，或 none / point_reduction_low / point_reduction_medium / point_reduction_high / pregeneration 等 HALCON 支持值。")]
        public string Optimization { get => optimization; set => SetProperty(ref optimization, value); }

        [DisplayName("极性度量")]
        [Description("匹配时对边缘极性的处理方式：使用极性、忽略全局极性或忽略局部极性。")]
        public ShapeModelMetric Metric { get => metric; set => SetProperty(ref metric, value); }

        [DisplayName("对比度")]
        [Description("提取模板边缘的对比度阈值。可填 auto，或单个数值，或 low,high,minSize 形式（如 30,60,15）。")]
        public string Contrast { get => contrast; set => SetProperty(ref contrast, value); }

        [DisplayName("最小对比度")]
        [Description("搜索时允许的最小对比度。可填 auto 或数值；通常不大于 Contrast。")]
        public string MinContrast { get => minContrast; set => SetProperty(ref minContrast, value); }

        public IHandleParameter Clone()
        {
            return new CreateScaledShapeModelParameter
            {
                NumLevels = NumLevels,
                AngleStartDeg = AngleStartDeg,
                AngleExtentDeg = AngleExtentDeg,
                AngleStepDeg = AngleStepDeg,
                ScaleMin = ScaleMin,
                ScaleMax = ScaleMax,
                ScaleStep = ScaleStep,
                Optimization = Optimization,
                Metric = Metric,
                Contrast = Contrast,
                MinContrast = MinContrast
            };
        }

        public void Reset()
        {
            NumLevels = 4;
            AngleStartDeg = -30;
            AngleExtentDeg = 60;
            AngleStepDeg = 0;
            ScaleMin = 0.8;
            ScaleMax = 1.2;
            ScaleStep = 0;
            Optimization = "auto";
            Metric = ShapeModelMetric.UsePolarity;
            Contrast = "auto";
            MinContrast = "auto";
        }
    }
}
