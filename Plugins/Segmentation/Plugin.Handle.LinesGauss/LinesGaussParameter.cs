using System.ComponentModel;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.LinesGauss
{
    public class LinesGaussParameter : BindableBase, IHandleParameter
    {
        private double sigma = 1.5;
        private double low = 3;
        private double high = 8;
        private ELightDark lightDark = ELightDark.dark;
        private bool extractWidth = true;
        private LinesGaussLineModel lineModel = LinesGaussLineModel.BarShaped;
        private bool completeJunctions = true;

        [DisplayName("Sigma")]
        [Description("高斯平滑尺度，值越大检测更粗的线条")]
        public double Sigma { get => sigma; set => SetProperty(ref sigma, value); }

        [DisplayName("低阈值")]
        [Description("线条响应低阈值")]
        public double Low { get => low; set => SetProperty(ref low, value); }

        [DisplayName("高阈值")]
        [Description("线条响应高阈值")]
        public double High { get => high; set => SetProperty(ref high, value); }

        [DisplayName("亮暗极性")]
        [Description("检测暗线条或亮线条")]
        public ELightDark LightDark { get => lightDark; set => SetProperty(ref lightDark, value); }

        [DisplayName("提取线宽")]
        [Description("是否同时提取线条宽度")]
        public bool ExtractWidth { get => extractWidth; set => SetProperty(ref extractWidth, value); }

        [DisplayName("线模型")]
        [Description("线条灰度剖面模型")]
        public LinesGaussLineModel LineModel { get => lineModel; set => SetProperty(ref lineModel, value); }

        [DisplayName("补全交叉点")]
        [Description("是否补全线条交叉点")]
        public bool CompleteJunctions { get => completeJunctions; set => SetProperty(ref completeJunctions, value); }

        public IHandleParameter Clone() => new LinesGaussParameter
        {
            Sigma = Sigma,
            Low = Low,
            High = High,
            LightDark = LightDark,
            ExtractWidth = ExtractWidth,
            LineModel = LineModel,
            CompleteJunctions = CompleteJunctions
        };

        public void Reset()
        {
            Sigma = 1.5;
            Low = 3;
            High = 8;
            LightDark = ELightDark.dark;
            ExtractWidth = true;
            LineModel = LinesGaussLineModel.BarShaped;
            CompleteJunctions = true;
        }
    }
}
