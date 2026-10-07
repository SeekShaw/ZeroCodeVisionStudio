using System.Collections.Generic;
using HalconDotNet;
using HalconVisionStudio.Shared;

namespace Plugin.Handle.Skeleton
{
    [Handle("骨架提取", "Skeleton", "提取输入区域的骨架（中心轴线）。", Common.HandleType_RegionProcessing)]
    public class SkeletonHandle : HandleBase, IHandleDataSlots
    {
        public SkeletonHandle(IDialogService dialogService) : base(dialogService)
        {
            HandleParameter = new EmptyParameter();
            SetRunParameter(null);
        }

        protected override string DialogName => Common.NoParameterPreviewDialogName;

        public IReadOnlyList<RunSlot> InputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Region, "输入区域", typeof(HObject))
        };

        public IReadOnlyList<RunSlot> OutputSlots { get; } = new[]
        {
            new RunSlot(RunOutputKeys.Output, "输出区域", typeof(HObject)),
            new RunSlot(RunOutputKeys.Region, "骨架区域", typeof(HObject))
        };

        public override void Handle()
        {
            var region = HandleInputGuard.RequireRegion(
                RunParameter?.GetOutput<HObject>(RunOutputKeys.Region) ?? RunParameter?.HOutput,
                "Skeleton");

            HOperatorSet.Skeleton(region, out var skeleton);
            RunParameter.HOutput = skeleton;
            RunParameter.SetOutput(RunOutputKeys.Output, skeleton);
            RunParameter.SetOutput(RunOutputKeys.Region, skeleton);
            RunParameter.ResultMessage = "骨架提取完成。";
            RaiseImageDisplay(skeleton);
        }
    }
}
