using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.ScaleImage
{
    [Handle("灰度拉伸", "ScaleImage", "通过乘数和加数调整图像灰度值。", Common.HandleType_Preprocessing)]
    public class ScaleImageHandle : ParameterHandleBase<ScaleImageParameter>, IHandleDataSlots
    {
        public ScaleImageHandle(IDialogService dialogService) : base(dialogService)
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
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "ScaleImage");

            HOperatorSet.ScaleImage(source, out var result, Parameter.Multiplier, Parameter.Add);
            RunParameter.HOutput = result;
            RunParameter.GrayImage = result;
            RunParameter.SetOutput(RunOutputKeys.Output, result);
            RunParameter.SetOutput(RunOutputKeys.GrayImage, result);
            RunParameter.ResultMessage = "灰度拉伸完成。";
            RaiseImageDisplay(result);
        }
    }
}
