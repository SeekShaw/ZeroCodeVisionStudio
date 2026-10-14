using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.HysteresisThreshold
{
    [Handle("滞后阈值分割", "HysteresisThreshold", "通过高低双阈值连接弱边缘的滞后阈值分割算子。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class HysteresisThresholdHandle : ParameterHandleBase<HysteresisThresholdParameter>, IHandleDataSlots
    {
        public HysteresisThresholdHandle(IDialogService dialogService) : base(dialogService) { }

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输入图像", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "区域", typeof(HObject))
        };

        public override void Handle()
        {
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "HysteresisThreshold");

            HOperatorSet.HysteresisThreshold(source, out var region, Parameter.Low, Parameter.High, Math.Max(1, Parameter.MaxLength));

            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = "滞后阈值分割完成。";
            RaiseImageDisplay(region);
        }
    }
}
