using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.BottomHat
{
    [Handle("底帽变换", "BottomHat", "突出图像中的暗色细小结构。", Common.HandleType_Preprocessing, OperatorBackend = OperatorBackend.OpenCV)]
    public class BottomHatHandle : ParameterHandleBase<BottomHatParameter>, IHandleDataSlots
    {
        public BottomHatHandle(IDialogService dialogService) : base(dialogService)
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
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "BottomHat");

            HOperatorSet.GenCircle(out var element, 0, 0, Math.Max(0.5, Parameter.Radius));
            HObject result = null;
            try
            {
                HOperatorSet.BottomHat(source, element, out result);
            }
            finally
            {
                element.Dispose();
            }

            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = "底帽变换完成。";
            RaiseImageDisplay(result);
        }
    }
}
