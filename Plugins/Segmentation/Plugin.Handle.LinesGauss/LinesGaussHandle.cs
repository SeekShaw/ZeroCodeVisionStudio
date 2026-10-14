using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.LinesGauss
{
    [Handle("高斯线条检测", "LinesGauss", "使用高斯滤波器提取图像中的线条 XLD 轮廓。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.Halcon)]
    public class LinesGaussHandle : ParameterHandleBase<LinesGaussParameter>, IHandleDataSlots
    {
        public LinesGaussHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "XLD 轮廓", typeof(HObject)),
            new RunSlot(RunOutputKeys.Xld, "XLD 轮廓", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "LinesGauss");

            HOperatorSet.LinesGauss(
                source, out var lines,
                Math.Max(0.1, Parameter.Sigma),
                Parameter.Low, Parameter.High,
                Parameter.LightDark.ToHalconString(),
                Parameter.ExtractWidth.ToHalconString(),
                Parameter.LineModel.ToHalconString(),
                Parameter.CompleteJunctions.ToHalconString());

            RunParameter.HOutput = lines;
            RunParameter.SetOutput(RunOutputKeys.Output, lines);
            RunParameter.SetOutput(RunOutputKeys.Xld, lines);
            RunParameter.ResultMessage = "高斯线条检测完成。";
            RaiseImageDisplay(lines);
        }
    }
}
