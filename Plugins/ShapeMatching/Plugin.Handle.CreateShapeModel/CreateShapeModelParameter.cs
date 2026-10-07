using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.CreateShapeModel
{
    public class CreateShapeModelParameter : BindableBase, IHandleParameter
    {
        private int numLevels = 4;
        private double angleStartDeg = -30;
        private double angleExtentDeg = 60;
        private double angleStepDeg = 0;
        private string optimization = "auto";
        private ShapeModelMetric metric = ShapeModelMetric.UsePolarity;
        private string contrast = "auto";
        private string minContrast = "auto";
        private string savePath = string.Empty;

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

        [DisplayName("保存路径")]
        [Description("模板创建后自动保存的路径，留空则不保存")]
        [FileDialog(FileDialogType.Save, "Shape Model (*.shm)|*.shm|All files (*.*)|*.*", ".shm")]
        public string SavePath { get => savePath; set => SetProperty(ref savePath, value); }

        public IHandleParameter Clone()
        {
            return new CreateShapeModelParameter
            {
                NumLevels = NumLevels,
                AngleStartDeg = AngleStartDeg,
                AngleExtentDeg = AngleExtentDeg,
                AngleStepDeg = AngleStepDeg,
                Optimization = Optimization,
                Metric = Metric,
                Contrast = Contrast,
                MinContrast = MinContrast,
                SavePath = SavePath
            };
        }

        public void Reset()
        {
            NumLevels = 4;
            AngleStartDeg = -30;
            AngleExtentDeg = 60;
            AngleStepDeg = 0;
            Optimization = "auto";
            Metric = ShapeModelMetric.UsePolarity;
            Contrast = "auto";
            MinContrast = "auto";
            SavePath = string.Empty;
        }
    }
}
