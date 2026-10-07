using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.LocalThreshold
{
    [Handle("局部阈值分割", "LocalThreshold", "基于局部统计的自适应阈值分割算子。", Common.HandleType_Segmentation)]
    public class LocalThresholdHandle : ParameterHandleBase<LocalThresholdParameter>, IHandleDataSlots
    {
        public LocalThresholdHandle(IDialogService dialogService) : base(dialogService) { }

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
            var source = HandleInputGuard.RequireImage(RunParameter?.GrayImage ?? RunParameter?.HOutput, "LocalThreshold");

            HOperatorSet.LocalThreshold(
                source, out var region,
                Math.Max(1, Parameter.MaskWidth), Math.Max(1, Parameter.MaskHeight),
                Parameter.Scale, Parameter.LightDark.ToHalconString());

            RunParameter.HOutput = region;
            RunParameter.SetOutput(RunOutputKeys.Output, region);
            RunParameter.SetOutput(RunOutputKeys.Region, region);
            RunParameter.ResultMessage = "局部阈值分割完成。";
            RaiseImageDisplay(region);
        }
    }
}
