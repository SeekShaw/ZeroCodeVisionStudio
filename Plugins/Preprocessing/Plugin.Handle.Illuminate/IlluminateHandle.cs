using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.Illuminate
{
    [Handle("光照校正", "Illuminate", "在分割前校正不均匀光照。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class IlluminateHandle : ParameterHandleBase<IlluminateParameter>, IHandleDataSlots
    {
        public IlluminateHandle(IDialogService dialogService) : base(dialogService)
        {
        }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出图像", typeof(HObject)),
            new RunSlot(RunOutputKeys.GrayImage, "灰度图像", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "Illuminate");

            HOperatorSet.Illuminate(
                source,
                out var result,
                Math.Max(1, Parameter.MaskWidth),
                Math.Max(1, Parameter.MaskHeight),
                Parameter.Factor);
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = "光照校正完成。";
            RaiseImageDisplay(result);
        }
    }
}
