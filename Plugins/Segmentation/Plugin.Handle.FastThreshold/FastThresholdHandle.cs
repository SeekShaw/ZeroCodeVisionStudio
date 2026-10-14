using HalconDotNet;
using ZeroCodeVisionStudio.Shared;

namespace Plugin.Handle.FastThreshold
{
    [Handle("快速阈值分割", "FastThreshold", "适用于大幅图像的快速灰度范围分割算子。", Common.HandleType_Segmentation, OperatorBackend = OperatorBackend.OpenCV)]
    public class FastThresholdHandle : ParameterHandleBase<FastThresholdParameter>, IHandleDataSlots
    {
        public FastThresholdHandle(IDialogService dialogService) : base(dialogService) { }

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
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "FastThreshold");

            HOperatorSet.FastThreshold(source, out var region, Parameter.MinGray, Parameter.MaxGray, Math.Max(1, Parameter.MinSize));

            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = "快速阈值分割完成。";
            RaiseImageDisplay(region);
        }
    }
}
